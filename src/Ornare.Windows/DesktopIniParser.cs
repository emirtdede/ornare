using System.Globalization;
using System.IO;
using System.Text;
using Ornare.Core;

namespace Ornare.Windows;

public sealed class DesktopIniParser
{
    private const string ShellClassInfoSection = "[.ShellClassInfo]";
    private const string IconResourceKey = "IconResource";
    private const string IconFileKey = "IconFile";
    private const string IconIndexKey = "IconIndex";

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

    public static string ReadFileWithEncodingFallback(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        // Fall back to UTF-8
        return Encoding.UTF8.GetString(bytes);
    }
}
