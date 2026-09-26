using System;
using System.IO;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class FolderColorApplyIntegrationTests : IDisposable
{
    private readonly ImagePipeline _pipeline = new();
    private readonly FolderCustomizer _customizer = new();
    private readonly string _testRoot;

    public FolderColorApplyIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"ornare_color_integ_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                var dir = new DirectoryInfo(_testRoot) { Attributes = FileAttributes.Normal };
                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void ApplyCuratedColorToFolderAndVerifyDurableRelativeIcon()
    {
        string targetFolder = Path.Combine(_testRoot, "ColoredTarget");
        Directory.CreateDirectory(targetFolder);

        string stagedIco = Path.Combine(_testRoot, "staged_color.ico");

        // 1. Render Blue #2563EB using FolderTemplateRenderer
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#2563EB", stagedIco);
        Assert.True(renderResult.IsSuccess, renderResult.ErrorMessage);
        Assert.True(File.Exists(stagedIco));

        // 2. Apply to folder via standard apply pipeline
        var applyResult = _customizer.ApplyIcon(targetFolder, stagedIco);
        Assert.True(applyResult.IsSuccess, applyResult.ErrorMessage);

        // 3. Verify folder attributes
        var dirInfo = new DirectoryInfo(targetFolder);
        Assert.True((dirInfo.Attributes & FileAttributes.ReadOnly) != 0);

        // 4. Verify durable hidden relative icon
        string ownedIco = Path.Combine(targetFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIco));
        var icoAttr = File.GetAttributes(ownedIco);
        Assert.True((icoAttr & FileAttributes.Hidden) != 0);

        // 5. Verify desktop.ini relative reference
        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        Assert.True(File.Exists(iniPath));
        string iniContent = File.ReadAllText(iniPath);
        Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniContent, StringComparison.OrdinalIgnoreCase);

        // 6. Delete staged ICO: target folder customization remains fully intact
        File.Delete(stagedIco);
        Assert.False(File.Exists(stagedIco));
        Assert.True(File.Exists(ownedIco));
    }

    [Fact]
    public void UndoRestoresOriginalDesktopIniAfterColorApply()
    {
        string targetFolder = Path.Combine(_testRoot, "UndoFolder");
        Directory.CreateDirectory(targetFolder);

        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nInfoTip=PreservedDescription\r\n");

        var prior = DesktopIniParser.ReadSemantics(targetFolder);
        var baseline = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = Guid.NewGuid(),
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        string stagedIco = Path.Combine(_testRoot, "staged_red.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#DC3545", stagedIco);
        Assert.True(renderResult.IsSuccess);

        var applyResult = _customizer.ApplyIcon(targetFolder, stagedIco);
        Assert.True(applyResult.IsSuccess);

        // Undo by resetting back to baseline
        var undoResult = _customizer.ResetIcon(targetFolder, baseline);
        Assert.True(undoResult.IsSuccess, undoResult.ErrorMessage);

        string afterUndoIni = File.ReadAllText(iniPath);
        Assert.DoesNotContain(@".ornare\folder.ico", afterUndoIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InfoTip=PreservedDescription", afterUndoIni, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResetCleansUpOwnedAssetsAndRestoresPreOrnareSemantics()
    {
        string targetFolder = Path.Combine(_testRoot, "ResetFolder");
        Directory.CreateDirectory(targetFolder);

        var prior = DesktopIniParser.ReadSemantics(targetFolder);
        var baseline = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = Guid.NewGuid(),
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        string stagedIco = Path.Combine(_testRoot, "staged_green.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#16A34A", stagedIco);
        Assert.True(renderResult.IsSuccess);

        var applyResult = _customizer.ApplyIcon(targetFolder, stagedIco);
        Assert.True(applyResult.IsSuccess);
        Assert.NotNull(applyResult.Value);

        prior = prior with { AttributesIntroduced = applyResult.Value.AttributesIntroduced };
        baseline = baseline with { PriorSemantics = prior };

        string ownedIco = Path.Combine(targetFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIco));

        // Reset
        var resetResult = _customizer.ResetIcon(targetFolder, baseline);
        Assert.True(resetResult.IsSuccess, resetResult.ErrorMessage);

        Assert.False(File.Exists(ownedIco));

        var dirInfo = new DirectoryInfo(targetFolder);
        dirInfo.Refresh();
        Assert.True((dirInfo.Attributes & FileAttributes.ReadOnly) == 0);
    }

    [Fact]
    public void FolderRenamePreservesCustomColor()
    {
        string originalFolder = Path.Combine(_testRoot, "OriginalFolderName");
        Directory.CreateDirectory(originalFolder);

        string stagedIco = Path.Combine(_testRoot, "staged_purple.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#7C3AED", stagedIco);
        Assert.True(renderResult.IsSuccess);

        var applyResult = _customizer.ApplyIcon(originalFolder, stagedIco);
        Assert.True(applyResult.IsSuccess);

        // Rename folder
        string renamedFolder = Path.Combine(_testRoot, "RenamedFolderName");
        Directory.Move(originalFolder, renamedFolder);

        string ownedIco = Path.Combine(renamedFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIco));

        string iniPath = Path.Combine(renamedFolder, "desktop.ini");
        Assert.True(File.Exists(iniPath));
        string iniContent = File.ReadAllText(iniPath);
        Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniContent, StringComparison.OrdinalIgnoreCase);
    }
}
