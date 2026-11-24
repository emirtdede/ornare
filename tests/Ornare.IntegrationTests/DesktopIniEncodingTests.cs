using System.IO;
using System.Text;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Proves that desktop.ini read/write preserves the original encoding (INV-FS-002).
/// <para>
/// Explorer reads desktop.ini in whatever encoding it finds; rewriting a legacy ANSI file as
/// UTF-16, or destroying a non-UTF-8 file with replacement characters, silently corrupts
/// folder metadata that Ornare does not own. These tests pin the round trip.
/// </para>
/// </summary>
public class DesktopIniEncodingTests
{
    private const string SampleContent =
        "[.ShellClassInfo]\r\nIconResource=.ornare\\folder.ico,0\r\n[ViewState]\r\nFolderType=Generic\r\n";

    [Fact]
    public void Utf16LittleEndianWithBomRoundTrips()
    {
        AssertRoundTrip(
            SampleContent,
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(SampleContent)).ToArray(),
            expectedEncoding: Encoding.Unicode,
            expectedPreamble: true);
    }

    [Fact]
    public void Utf16BigEndianWithBomRoundTrips()
    {
        AssertRoundTrip(
            SampleContent,
            Encoding.BigEndianUnicode.GetPreamble()
                .Concat(Encoding.BigEndianUnicode.GetBytes(SampleContent))
                .ToArray(),
            expectedEncoding: Encoding.BigEndianUnicode,
            expectedPreamble: true);
    }

    [Fact]
    public void Utf8WithBomRoundTrips()
    {
        AssertRoundTrip(
            SampleContent,
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(SampleContent)).ToArray(),
            expectedEncoding: Encoding.UTF8,
            expectedPreamble: true);
    }

    [Fact]
    public void Utf8WithoutBomRoundTripsAndStaysBomless()
    {
        AssertRoundTrip(
            SampleContent,
            Encoding.UTF8.GetBytes(SampleContent),
            expectedEncoding: Encoding.UTF8,
            expectedPreamble: false);
    }

    [Fact]
    public void InvalidUtf8IsTreatedAsLegacyAnsiAndNotCorrupted()
    {
        // 0xE9 is not valid UTF-8 on its own, so the file must be read through the ANSI path.
        byte[] bytes = Encoding.ASCII.GetBytes("[.ShellClassInfo]\r\nInfoTip=")
            .Concat(new byte[] { 0xE9 })
            .Concat(Encoding.ASCII.GetBytes("\r\n"))
            .ToArray();

        string path = WriteTemp(bytes);
        try
        {
            DesktopIniReadResult result = DesktopIniParser.ReadFileWithEncoding(path);

            Assert.True(result.IsSystemAnsi);
            Assert.False(result.EmitPreamble);
            Assert.DoesNotContain('\uFFFD', result.Text);

            // Re-encoding through the same ANSI code page must reproduce the original bytes.
            byte[] reEncoded = DesktopIniParser.EncodeForWrite(result.Text, result);
            Assert.Equal(bytes, reEncoded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFileWithEncodingRejectsOversizedFile()
    {
        string path = WriteTemp(new byte[DesktopIniParser.MaxIniBytes + 1]);
        try
        {
            Assert.Throws<IOException>(() => DesktopIniParser.ReadFileWithEncoding(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertRoundTrip(
        string content,
        byte[] originalBytes,
        Encoding expectedEncoding,
        bool expectedPreamble)
    {
        string path = WriteTemp(originalBytes);
        try
        {
            DesktopIniReadResult read = DesktopIniParser.ReadFileWithEncoding(path);
            Assert.Equal(content, read.Text);
            Assert.False(read.IsSystemAnsi);
            Assert.Equal(expectedEncoding.WebName, read.Encoding.WebName);
            Assert.Equal(expectedPreamble, read.EmitPreamble);

            byte[] reEncoded = DesktopIniParser.EncodeForWrite(read.Text, read);
            Assert.Equal(originalBytes, reEncoded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemp(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ornare_desktopini_{System.Guid.NewGuid():N}.ini");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
