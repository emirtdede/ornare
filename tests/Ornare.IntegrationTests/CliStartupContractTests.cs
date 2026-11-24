using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Cli;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Audit finding 4.1. The CLI installed the DirectWrite emoji rasterizer, built the whole
/// command line and touched storage before it knew what the user asked for, so read-only
/// commands paid for work they never used and the process was slower than the packaged alias
/// for the same job.
/// </summary>
public sealed class CliStartupContractTests
{
    [Fact]
    public async Task VersionIsAnsweredWithoutASubcommand()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            int code = await Program.Main(["--version"]);
            Assert.Equal(CliExitCodes.Success, code);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string output = writer.ToString().Trim();
        Assert.StartsWith("ornare ", output, StringComparison.Ordinal);
        Assert.Contains("schema 1", output, StringComparison.Ordinal);

        // The build hash must not leak into a version a user reads.
        Assert.DoesNotContain("+", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RasterizerIsInstalledLazilyAndOnlyByRenderingCommands()
    {
        string root = FindRepositoryRoot();
        string program = File.ReadAllText(Path.Combine(root, "src", "Ornare.Cli", "Program.cs"));

        // The eager assignment at process start is what made `--help` allocate DirectWrite.
        Assert.DoesNotContain(
            "EmojiRasterizer = new Ornare.Windows.DirectWriteEmojiRasterizer();",
            program.Split("private static void EnsureRenderingConfigured()")[0]);

        string gate = "private static void EnsureRenderingConfigured()";
        Assert.Contains(gate, program);
        Assert.Contains("Interlocked.Exchange(ref _renderingConfigured, 1)", program);

        // Exactly the four commands that render an icon may install it.
        Assert.Equal(4, CountOccurrences(program, "EnsureRenderingConfigured();"));

        foreach (string command in new[]
        {
            "HandleApplyAsync",
            "HandleApplyLastAsync",
            "HandleExportIcoAsync",
            "HandleExportPngAsync"
        })
        {
            int callIndex = program.IndexOf($"return await handlers.{command}(", StringComparison.Ordinal);
            Assert.True(callIndex > 0, $"{command} call site missing.");
            int gateIndex = program.LastIndexOf("EnsureRenderingConfigured();", callIndex, StringComparison.Ordinal);
            Assert.True(gateIndex > 0, $"{command} does not configure rendering.");
            Assert.True(
                callIndex - gateIndex < 400,
                $"{command} must configure rendering immediately before creating the handler.");
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Ornare.slnx")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException("Could not find the repository root from " + AppContext.BaseDirectory);
    }
}
