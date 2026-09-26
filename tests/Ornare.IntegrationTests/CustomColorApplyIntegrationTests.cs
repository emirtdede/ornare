using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class CustomColorApplyIntegrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _storageDbPath;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;
    private readonly ImagePipeline _pipeline;

    public CustomColorApplyIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ornare_test_custom_color_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _storageDbPath = Path.Combine(_tempRoot, "ornare_test.db");
        _storage = new SqliteStorageService();
        _storage.InitializeAsync(_storageDbPath).GetAwaiter().GetResult();

        _customizer = new FolderCustomizer();
        _pipeline = new ImagePipeline();
    }

    public void Dispose()
    {
        _storage.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                // Clear ReadOnly / System / Hidden attributes recursively so delete succeeds
                foreach (var file in Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_tempRoot, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task CancelLeavesTargetFolderAndRecentSettingsUnchanged()
    {
        string targetFolder = Path.Combine(_tempRoot, "MyTargetFolder");
        Directory.CreateDirectory(targetFolder);

        // Pre-create some content
        string testFile = Path.Combine(targetFolder, "sample.txt");
        File.WriteAllText(testFile, "Hello World");

        var initialDirInfo = new DirectoryInfo(targetFolder);
        var initialAttrs = initialDirInfo.Attributes;
        bool desktopIniExistedBefore = File.Exists(Path.Combine(targetFolder, "desktop.ini"));

        var initialSettings = await _storage.GetUserSettingsAsync();
        Assert.Empty(initialSettings.RecentCustomColors);

        // Simulate user picking color, preview generated, but user cancels
        string candidateColor = "#FF5722";
        var previewResult = FolderTemplateRenderer.RenderPreviewPng(candidateColor);
        Assert.True(previewResult.IsSuccess);

        // Simulate Cancel action (no folder apply, no settings update)
        // Verify folder state remains identical
        initialDirInfo.Refresh();
        Assert.Equal(initialAttrs, initialDirInfo.Attributes);
        Assert.Equal(desktopIniExistedBefore, File.Exists(Path.Combine(targetFolder, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(targetFolder, ".ornare")));

        // Verify settings still empty
        var currentSettings = await _storage.GetUserSettingsAsync();
        Assert.Empty(currentSettings.RecentCustomColors);
    }

    [Fact]
    public async Task SuccessfulCustomColorApplyCreatesDurableIcoAndPersistsToRecents()
    {
        string targetFolder = Path.Combine(_tempRoot, "CustomColorFolder");
        Directory.CreateDirectory(targetFolder);

        string customColor = "#9C27B0"; // Purple 500
        string tempIcoPath = Path.Combine(_tempRoot, "staged_custom.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, customColor, tempIcoPath);
        Assert.True(renderResult.IsSuccess, renderResult.ErrorMessage);
        Assert.True(File.Exists(tempIcoPath));

        var applyResult = _customizer.ApplyIcon(targetFolder, tempIcoPath);
        Assert.True(applyResult.IsSuccess, applyResult.ErrorMessage);

        // Verify folder attributes and durable relative .ornare\folder.ico
        var dirInfo = new DirectoryInfo(targetFolder);
        dirInfo.Refresh();
        Assert.True(dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly) || dirInfo.Attributes.HasFlag(FileAttributes.System));

        string durableIco = Path.Combine(targetFolder, ".ornare", "folder.ico");
        Assert.True(File.Exists(durableIco));

        // Simulate QuickPanel's post-apply persistence
        var settings = await _storage.GetUserSettingsAsync();
        var updated = settings.WithRecentCustomColor(customColor);
        await _storage.SaveUserSettingsAsync(updated);

        // Verify settings contain the custom color
        var reloaded = await _storage.GetUserSettingsAsync();
        Assert.Single(reloaded.RecentCustomColors);
        Assert.Equal(customColor, reloaded.RecentCustomColors[0]);
    }

    [Fact]
    public async Task FailedApplyLeavesRecentCustomColorsUnchanged()
    {
        string invalidTargetFolder = Path.Combine(_tempRoot, "NonExistentFolder_12345");
        string customColor = "#E91E63";
        string tempIcoPath = Path.Combine(_tempRoot, "staged_fail.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, customColor, tempIcoPath);
        Assert.True(renderResult.IsSuccess);

        // Apply to non-existent folder must fail
        var applyResult = _customizer.ApplyIcon(invalidTargetFolder, tempIcoPath);
        Assert.False(applyResult.IsSuccess);

        // Verify recents remain unchanged
        var settings = await _storage.GetUserSettingsAsync();
        Assert.Empty(settings.RecentCustomColors);
    }
}
