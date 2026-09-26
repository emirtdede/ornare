using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Ornare.Cli;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class CliGrammarValidationTests
{
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var swOut = new StringWriter();
        using var swErr = new StringWriter();

        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);

            int code = await Program.Main(args);
            return (code, swOut.ToString().Trim(), swErr.ToString().Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task NoArgumentsWithJsonReturnsExitCode2AndSchemaVersion1()
    {
        var (code, stdOut, _) = await RunCliAsync("--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("InvalidArguments", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyMissingFolderReturnsExitCode2()
    {
        var (code, stdOut, _) = await RunCliAsync("apply", "--color", "#0078D4", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyMissingRenderSelectorReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
        Assert.Contains("Exactly one render input", doc.RootElement.GetProperty("messageKey").GetString());
    }

    [Fact]
    public async Task ApplyConflictingSelectorsReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--color", "#0078D4", "--preset", "status.done", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
        Assert.Contains("Conflicting render inputs", doc.RootElement.GetProperty("messageKey").GetString());
    }

    [Fact]
    public async Task PresetWithTemplateModifierReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--preset", "status.done", "--template", "ornare-soft", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
        Assert.Contains("not permitted with --preset", doc.RootElement.GetProperty("messageKey").GetString());
    }

    [Fact]
    public async Task PresetWithModeModifierReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--preset", "status.done", "--mode", "cover", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PresetUnknownIdReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--preset", "nonexistent.preset.id", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
        Assert.Contains("Unknown preset ID", doc.RootElement.GetProperty("messageKey").GetString());
    }

    [Fact]
    public async Task ColorWithModeModifierReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--color", "#0078D4", "--mode", "contain", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ColorWithPaddingModifierReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--color", "#0078D4", "--padding", "10", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ColorInvalidHexFormatReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--color", "invalid-hex", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ColorInvalidTemplateReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--color", "#0078D4", "--template", "unsupported-template", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SourceWithTemplateModifierReturnsExitCode2()
    {
        string dummy = Path.GetTempPath();
        var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--source", "test.png", "--template", "ornare-flat", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SourceInvalidModeReturnsExitCode2()
    {
        string dummyFile = Path.Combine(Path.GetTempPath(), $"source_test_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(dummyFile, [1, 2, 3]);

        try
        {
            string dummy = Path.GetTempPath();
            var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--source", dummyFile, "--mode", "stretch", "--json");

            Assert.Equal(2, code);
            using var doc = JsonDocument.Parse(stdOut);
            Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            if (File.Exists(dummyFile)) File.Delete(dummyFile);
        }
    }

    [Fact]
    public async Task SourcePaddingOutOfRangeReturnsExitCode2()
    {
        string dummyFile = Path.Combine(Path.GetTempPath(), $"source_test_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(dummyFile, [1, 2, 3]);

        try
        {
            string dummy = Path.GetTempPath();
            var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--source", dummyFile, "--padding", "35", "--json");

            Assert.Equal(2, code);
            using var doc = JsonDocument.Parse(stdOut);
            Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            if (File.Exists(dummyFile)) File.Delete(dummyFile);
        }
    }

    [Fact]
    public async Task SourceNonSvgWithRecolorReturnsExitCode2()
    {
        string dummyFile = Path.Combine(Path.GetTempPath(), $"source_test_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(dummyFile, [1, 2, 3]);

        try
        {
            string dummy = Path.GetTempPath();
            var (code, stdOut, _) = await RunCliAsync("apply", dummy, "--source", dummyFile, "--recolor", "#16A34A", "--json");

            Assert.Equal(2, code);
            using var doc = JsonDocument.Parse(stdOut);
            Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
            Assert.Contains("only with SVG sources", doc.RootElement.GetProperty("messageKey").GetString());
        }
        finally
        {
            if (File.Exists(dummyFile)) File.Delete(dummyFile);
        }
    }

    [Fact]
    public async Task ExportIcoMissingOutputReturnsExitCode2()
    {
        var (code, stdOut, _) = await RunCliAsync("export-ico", "--color", "#0078D4", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExportPngInvalidSizeReturnsExitCode2()
    {
        var (code, stdOut, _) = await RunCliAsync("export-png", "--output", "out.png", "--color", "#0078D4", "--size", "300", "--json");

        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }
}
