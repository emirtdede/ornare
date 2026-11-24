using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Result of reading a <c>desktop.ini</c> file: the decoded text together with the exact
/// encoding that must be used to write it back, so a reset/undo never silently converts a
/// third-party ANSI file into UTF-16.
/// </summary>
public readonly record struct DesktopIniReadResult(
    string Text,
    Encoding Encoding,
    bool EmitPreamble,
    bool IsSystemAnsi = false);

public sealed class DesktopIniParser
{
    private const string ShellClassInfoSection = "[.ShellClassInfo]";
    private const string IconResourceKey = "IconResource";
    private const string IconFileKey = "IconFile";
    private const string IconIndexKey = "IconIndex";

    /// <summary>
    /// A <c>desktop.ini</c> is a small INI file. Anything beyond this bound indicates a
    /// corrupt or unrelated file and must not be pulled into memory in full.
    /// </summary>
    public const int MaxIniBytes = 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Reverse of the active ANSI code page, built once per process. Building it costs 256
    /// <c>MultiByteToWideChar</c> calls, so it is shared instead of rebuilt on every write.
    /// </summary>
    private static readonly Lazy<Dictionary<char, byte>> AnsiEncodeMap = new(BuildAnsiEncodeMap);

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int MultiByteToWideChar(
        uint codePage,
        uint dwFlags,
        byte[] lpMultiByteStr,
        int cbMultiByte,
        IntPtr lpWideCharStr,
        int cchWideChar);

    public static FolderIconSemantics ReadSemantics(string folderPath)
    {
        string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
        if (!File.Exists(iniPath))
        {
            return new FolderIconSemantics();
        }

        try
        {
            string content = ReadFileWithEncodingFallback(iniPath);
            return ParseSemanticsFromContent(content);
        }
        catch
        {
            return new FolderIconSemantics();
        }
    }

    public static FolderIconSemantics ParseSemanticsFromContent(string content)
    {
        string? iconResource = null;
        string? iconFile = null;
        int? iconIndex = null;
        StringBuilder unrelated = new();

        bool inShellClassInfo = false;

        using StringReader reader = new(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inShellClassInfo = string.Equals(trimmed, ShellClassInfoSection, StringComparison.OrdinalIgnoreCase);
                if (!inShellClassInfo)
                {
                    unrelated.AppendLine(line);
                }
                continue;
            }

            if (inShellClassInfo)
            {
                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    string key = trimmed[..eqIndex].Trim();
                    string val = trimmed[(eqIndex + 1)..].Trim();

                    if (string.Equals(key, IconResourceKey, StringComparison.OrdinalIgnoreCase))
                    {
                        iconResource = val;
                    }
                    else if (string.Equals(key, IconFileKey, StringComparison.OrdinalIgnoreCase))
                    {
                        iconFile = val;
                    }
                    else if (string.Equals(key, IconIndexKey, StringComparison.OrdinalIgnoreCase) && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                    {
                        iconIndex = idx;
                    }
                    else
                    {
                        unrelated.AppendLine(line);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    unrelated.AppendLine(line);
                }
            }
            else
            {
                unrelated.AppendLine(line);
            }
        }

        return new FolderIconSemantics
        {
            IconResource = iconResource,
            IconFile = iconFile,
            IconIndex = iconIndex,
            RawUnrelatedContent = unrelated.ToString().TrimEnd()
        };
    }

    public static string MergeSemanticsForApply(string existingContent, string relativeIcoPath)
    {
        StringBuilder output = new();
        bool inShellClassInfo = false;
        bool shellClassInfoWritten = false;
        List<string> preservedShellClassInfoLines = [];

        using (StringReader reader = new(existingContent))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    if (inShellClassInfo)
                    {
                        WriteShellClassInfoSection(output, relativeIcoPath, preservedShellClassInfoLines);
                        shellClassInfoWritten = true;
                        inShellClassInfo = false;
                    }

                    if (string.Equals(trimmed, ShellClassInfoSection, StringComparison.OrdinalIgnoreCase))
                    {
                        inShellClassInfo = true;
                        continue;
                    }

                    output.AppendLine(line);
                    continue;
                }

                if (inShellClassInfo)
                {
                    int eqIndex = trimmed.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string key = trimmed[..eqIndex].Trim();
                        // Filter out existing icon keys
                        if (!string.Equals(key, IconResourceKey, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(key, IconFileKey, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(key, IconIndexKey, StringComparison.OrdinalIgnoreCase))
                        {
                            preservedShellClassInfoLines.Add(line);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        preservedShellClassInfoLines.Add(line);
                    }
                }
                else
                {
                    output.AppendLine(line);
                }
            }
        }

        if (inShellClassInfo || !shellClassInfoWritten)
        {
            WriteShellClassInfoSection(output, relativeIcoPath, preservedShellClassInfoLines);
        }

        return output.ToString();
    }

    public static string MergeSemanticsForReset(string existingContent, FolderIconSemantics priorSemantics)
    {
        StringBuilder output = new();
        bool inShellClassInfo = false;
        bool shellClassInfoWritten = false;
        List<string> preservedShellClassInfoLines = [];

        using (StringReader reader = new(existingContent))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    if (inShellClassInfo)
                    {
                        WriteRestoredShellClassInfoSection(output, priorSemantics, preservedShellClassInfoLines);
                        shellClassInfoWritten = true;
                        inShellClassInfo = false;
                    }

                    if (string.Equals(trimmed, ShellClassInfoSection, StringComparison.OrdinalIgnoreCase))
                    {
                        inShellClassInfo = true;
                        continue;
                    }

                    output.AppendLine(line);
                    continue;
                }

                if (inShellClassInfo)
                {
                    int eqIndex = trimmed.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string key = trimmed[..eqIndex].Trim();
                        // Filter out existing icon keys
                        if (!string.Equals(key, IconResourceKey, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(key, IconFileKey, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(key, IconIndexKey, StringComparison.OrdinalIgnoreCase))
                        {
                            preservedShellClassInfoLines.Add(line);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        preservedShellClassInfoLines.Add(line);
                    }
                }
                else
                {
                    output.AppendLine(line);
                }
            }
        }

        if (inShellClassInfo || (!shellClassInfoWritten && priorSemantics.HasCustomIcon))
        {
            WriteRestoredShellClassInfoSection(output, priorSemantics, preservedShellClassInfoLines);
        }

        return output.ToString();
    }

    private static void WriteShellClassInfoSection(StringBuilder sb, string relativeIcoPath, List<string> preservedLines)
    {
        sb.AppendLine(ShellClassInfoSection);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{IconResourceKey}={relativeIcoPath},0");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{IconFileKey}={relativeIcoPath}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{IconIndexKey}=0");
        foreach (string line in preservedLines)
        {
            sb.AppendLine(line);
        }
    }

    private static void WriteRestoredShellClassInfoSection(StringBuilder sb, FolderIconSemantics priorSemantics, List<string> preservedLines)
    {
        if (priorSemantics.HasCustomIcon || preservedLines.Count > 0)
        {
            sb.AppendLine(ShellClassInfoSection);
            if (!string.IsNullOrWhiteSpace(priorSemantics.IconResource))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{IconResourceKey}={priorSemantics.IconResource}");
            }
            if (!string.IsNullOrWhiteSpace(priorSemantics.IconFile))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{IconFileKey}={priorSemantics.IconFile}");
            }
            if (priorSemantics.IconIndex.HasValue)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{IconIndexKey}={priorSemantics.IconIndex.Value}");
            }
            foreach (string line in preservedLines)
            {
                sb.AppendLine(line);
            }
        }
    }

    /// <summary>
    /// Decodes <paramref name="filePath"/> and reports the encoding that must be used to write
    /// it back. UTF-8 is only assumed when the bytes really are valid UTF-8; legacy ANSI files
    /// (for example Windows-1254 Turkish) are decoded with the system ANSI code page so their
    /// preserved metadata survives intact instead of becoming <c>U+FFFD</c>.
    /// </summary>
    public static DesktopIniReadResult ReadFileWithEncoding(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);

        if (bytes.Length > MaxIniBytes)
        {
            throw new IOException(
                $"desktop.ini exceeds the supported size ({bytes.Length} > {MaxIniBytes} bytes).");
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return new DesktopIniReadResult(
                Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2),
                Encoding.Unicode,
                EmitPreamble: true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return new DesktopIniReadResult(
                Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2),
                Encoding.BigEndianUnicode,
                EmitPreamble: true);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new DesktopIniReadResult(
                Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3),
                Encoding.UTF8,
                EmitPreamble: true);
        }

        try
        {
            // No BOM: valid UTF-8 is UTF-8. Invalid UTF-8 is legacy ANSI, not a reason to
            // destroy the user's text with replacement characters.
            return new DesktopIniReadResult(
                StrictUtf8.GetString(bytes),
                Encoding.UTF8,
                EmitPreamble: false);
        }
        catch (DecoderFallbackException)
        {
            return new DesktopIniReadResult(
                DecodeSystemAnsi(bytes),
                Encoding.UTF8,
                EmitPreamble: false,
                IsSystemAnsi: true);
        }
    }

    public static string ReadFileWithEncodingFallback(string filePath)
        => ReadFileWithEncoding(filePath).Text;

    /// <summary>Decodes legacy single-byte text with the active Windows ANSI code page.</summary>
    private static string DecodeSystemAnsi(byte[] bytes)
    {
        uint codePage = GetACP();
        int needed = MultiByteToWideChar(codePage, 0, bytes, bytes.Length, IntPtr.Zero, 0);
        if (needed <= 0)
        {
            return Encoding.Latin1.GetString(bytes);
        }

        // The native function writes UTF-16 into caller-owned memory. Using an explicit buffer
        // instead of an array parameter keeps the marshaling unambiguous and cannot corrupt the
        // managed heap if the count is ever unexpected.
        IntPtr buffer = Marshal.AllocHGlobal(checked(needed * sizeof(char)));
        try
        {
            int written = MultiByteToWideChar(codePage, 0, bytes, bytes.Length, buffer, needed);
            return written <= 0
                ? Encoding.Latin1.GetString(bytes)
                : Marshal.PtrToStringUni(buffer, written) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Serialises <paramref name="content"/> with an explicit preamble when the original file
    /// carried one; callers write the bytes atomically so a crash cannot truncate desktop.ini.
    /// </summary>
    public static byte[] EncodeForWrite(string content, DesktopIniReadResult encodingHint)
    {
        if (encodingHint.IsSystemAnsi)
        {
            // A legacy ANSI file is written back in the same code page when every character is
            // representable. If it is not, writing a replacement byte would corrupt metadata, so
            // the content is preserved by promoting the file to UTF-8 with a BOM instead.
            return TryEncodeSystemAnsi(content, out byte[] ansiBytes)
                ? ansiBytes
                : [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(content)];
        }

        byte[] body = encodingHint.Encoding.GetBytes(content);

        if (!encodingHint.EmitPreamble)
        {
            return body;
        }

        byte[] preamble = encodingHint.Encoding.GetPreamble();
        return preamble.Length == 0 ? body : [.. preamble, .. body];
    }

    /// <summary>
    /// Encodes text with the active Windows ANSI code page. The reverse map is built from
    /// <c>MultiByteToWideChar</c>, the same API that decoded the file, so decode and encode stay
    /// exactly symmetric for every single-byte code page. Turkish (1254), Central European (1250)
    /// and Western European (1252) files therefore round-trip byte for byte.
    /// </summary>
    private static bool TryEncodeSystemAnsi(string content, out byte[] bytes)
    {
        Dictionary<char, byte> map = AnsiEncodeMap.Value;
        byte[] result = new byte[content.Length];
        for (int i = 0; i < content.Length; i++)
        {
            if (!map.TryGetValue(content[i], out byte value))
            {
                bytes = [];
                return false;
            }

            result[i] = value;
        }

        bytes = result;
        return true;
    }

    private static Dictionary<char, byte> BuildAnsiEncodeMap()
    {
        Dictionary<char, byte> map = new(256);
        for (int value = 0; value < 256; value++)
        {
            string decoded = DecodeSystemAnsi([(byte)value]);
            if (decoded.Length == 1)
            {
                map.TryAdd(decoded[0], (byte)value);
            }
        }

        return map;
    }
}
