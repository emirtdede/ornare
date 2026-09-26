using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public class ApplyLastIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _testDbPath;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;
    private readonly ImagePipeline _pipeline;

    public ApplyLastIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ornare_apply_last_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _testDbPath = Path.Combine(_testRoot, "test_ornare.db");
        _storage = new SqliteStorageService();
        _storage.InitializeAsync(_testDbPath).GetAwaiter().GetResult();
        _customizer = new FolderCustomizer();
        _pipeline = new ImagePipeline();
    }

    public void Dispose()
    {
        _storage.Dispose();
        if (Directory.Exists(_testRoot))
        {
            try
            {
                // Clear ReadOnly attributes if any before directory deletion
                foreach (var dir in Directory.EnumerateDirectories(_testRoot, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(dir, FileAttributes.Normal); } catch { }
                }
                foreach (var file in Directory.EnumerateFiles(_testRoot, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(_testRoot, recursive: true);
            }
            catch { }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ApplyLastWhenNoLastAppliedKeyReturnsNoLastAppliedRecipe()
    {
        string targetFolder = Path.Combine(_testRoot, "FolderNoLast");
        Directory.CreateDirectory(targetFolder);

        var result = await ApplyLastService.ApplyLastAsync(targetFolder, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NoLastAppliedRecipe, result.ErrorCode);
    }

    [Fact]
    public async Task ApplyLastAfterFolderApplyAppliesIdenticalIconSemanticsToSecondFolder()
    {
        // 1. Setup Folder A and apply an icon
        string folderA = Path.Combine(_testRoot, "FolderA");
        Directory.CreateDirectory(folderA);

        string templateId = FolderTemplateCatalog.DefaultTemplateId;
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion;
        string hexColor = "#0078D4";
        string stagedIco = Path.Combine(_testRoot, "staged_color.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            hexColor,
            stagedIco,
            templateId,
            templateVersion);
        Assert.True(renderResult.IsSuccess);
        Assert.NotNull(renderResult.Value);
        string renderKey = renderResult.Value.RenderKey!;
        Assert.False(string.IsNullOrWhiteSpace(renderKey));

        // Save render cache entry and set as LastAppliedRenderKey
        string canonicalIco = Path.Combine(_testRoot, $"{renderResult.Value.ContentHash}.ico");
        File.Copy(stagedIco, canonicalIco, overwrite: true);

        var cacheEntry = new RenderCacheEntry
        {
            RenderKey = renderKey,
            RecipeVersion = 1,
            RecipeJson = FolderTemplateRenderer.CreateRecipe(hexColor, templateId, templateVersion).ToJson(),
            SourceAssetHash = renderResult.Value.ContentHash,
            CanonicalIcoPath = canonicalIco,
            FrameSizes = string.Join(",", renderResult.Value.FrameSizes),
            FileSizeBytes = new FileInfo(canonicalIco).Length,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        };
        await _storage.SaveRenderCacheEntryAsync(cacheEntry);
        await _storage.SetLastAppliedRenderKeyAsync(renderKey);

        // Apply icon to Folder A
        var applyAResult = _customizer.ApplyIcon(folderA, stagedIco);
        Assert.True(applyAResult.IsSuccess);

        // 2. Folder B: Execute ApplyLastAsync
        string folderB = Path.Combine(_testRoot, "FolderB");
        Directory.CreateDirectory(folderB);

        var applyLastResult = await ApplyLastService.ApplyLastAsync(folderB, _storage, _customizer);
        Assert.True(applyLastResult.IsSuccess, applyLastResult.ErrorMessage);

        // 3. Verify Folder B has identical relative icon semantics
        string ownedIcoB = Path.Combine(folderB, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIcoB));

        string ownedIcoA = Path.Combine(folderA, @".ornare\folder.ico");
        byte[] bytesA = await File.ReadAllBytesAsync(ownedIcoA);
        byte[] bytesB = await File.ReadAllBytesAsync(ownedIcoB);
        Assert.Equal(bytesA, bytesB);

        // Verify Folder B desktop.ini semantics
        var semanticsB = DesktopIniParser.ReadSemantics(folderB);
        Assert.Equal(@".ornare\folder.ico,0", semanticsB.IconResource);
        Assert.Equal(0, semanticsB.IconIndex);

        // Verify folder assignment was recorded
        var assignmentB = await _storage.GetAssignmentByPathAsync(folderB);
        Assert.NotNull(assignmentB);
        Assert.Equal(FolderCustomizationState.Healthy, assignmentB.Status);
    }

    [Fact]
    public async Task ApplyLastWhenCacheFileDeletedRebuildsFromRecipeAndApplies()
    {
        string hexColor = "#E81123";
        string templateId = FolderTemplateCatalog.DefaultTemplateId;
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion;
        var recipe = FolderTemplateRenderer.CreateRecipe(hexColor, templateId, templateVersion);
        string renderKey = recipe.ComputeRenderKey();

        string canonicalIco = Path.Combine(_testRoot, "deleted_cache.ico");
        // Create initially then deliberately delete
        var initialRender = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, hexColor, canonicalIco, templateId, templateVersion);
        Assert.True(initialRender.IsSuccess);
        Assert.True(File.Exists(canonicalIco));

        var cacheEntry = new RenderCacheEntry
        {
            RenderKey = renderKey,
            RecipeVersion = 1,
            RecipeJson = recipe.ToJson(),
            SourceAssetHash = recipe.SourceAssetHash,
            CanonicalIcoPath = canonicalIco,
            FrameSizes = "16,20,24,32,40,48,64,96,128,256",
            FileSizeBytes = new FileInfo(canonicalIco).Length,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        };
        await _storage.SaveRenderCacheEntryAsync(cacheEntry);
        await _storage.SetLastAppliedRenderKeyAsync(renderKey);

        // Now delete the cached ICO to simulate missing derived cache
        File.Delete(canonicalIco);
        Assert.False(File.Exists(canonicalIco));

        // Rebuild function that reconstructs the ICO from the retained recipe
        Func<RenderCacheEntry, Task<string?>> rebuildFunc = entry =>
        {
            var r = RenderRecipe.FromJson(entry.RecipeJson);
            if (r == null) return Task.FromResult<string?>(null);

            var descriptor = FolderTemplateCatalog.GetTemplate(r.TemplateId, r.TemplateVersion);
            byte[] tBytes = descriptor.GetBytes();
            var res = _pipeline.ProcessRecipe(r, tBytes, entry.CanonicalIcoPath);
            return Task.FromResult<string?>(res.IsSuccess ? entry.CanonicalIcoPath : null);
        };

        // Target folder
        string targetFolder = Path.Combine(_testRoot, "FolderRebuild");
        Directory.CreateDirectory(targetFolder);

        // Execute ApplyLastAsync with rebuild func
        var result = await ApplyLastService.ApplyLastAsync(targetFolder, _storage, _customizer, rebuildFunc);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        // Verify cache file was rebuilt on disk
        Assert.True(File.Exists(canonicalIco));

        // Verify folder icon was applied
        string targetIco = Path.Combine(targetFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(targetIco));
    }

    [Fact]
    public async Task ApplyLastWhenPointerIsCorruptOrUnresolvableTransactionallyClearsPointerAndReturnsTypedError()
    {
        // Set LastAppliedRenderKey to a bogus non-existent key
        string bogusKey = "bogus_render_key_that_does_not_exist";
        await _storage.SetLastAppliedRenderKeyAsync(bogusKey);
        Assert.Equal(bogusKey, await _storage.GetLastAppliedRenderKeyAsync());

        string targetFolder = Path.Combine(_testRoot, "FolderCorruptPointer");
        Directory.CreateDirectory(targetFolder);

        var result = await ApplyLastService.ApplyLastAsync(targetFolder, _storage, _customizer);

        // Must fail with typed ErrorCode.NoLastAppliedRecipe
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NoLastAppliedRecipe, result.ErrorCode);

        // Must have cleared the corrupt pointer
        var clearedKey = await _storage.GetLastAppliedRenderKeyAsync();
        Assert.Null(clearedKey);
    }

    [Fact]
    public async Task ApplyLastRejectsInvalidTargetsFileAndNonExistent()
    {
        string hexColor = "#107C41";
        var recipe = FolderTemplateRenderer.CreateRecipe(hexColor);
        string renderKey = recipe.ComputeRenderKey();
        string icoPath = Path.Combine(_testRoot, "valid_for_validation.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, hexColor, icoPath);
        Assert.True(renderResult.IsSuccess);

        await _storage.SaveRenderCacheEntryAsync(new RenderCacheEntry
        {
            RenderKey = renderKey,
            RecipeVersion = 1,
            RecipeJson = recipe.ToJson(),
            SourceAssetHash = recipe.SourceAssetHash,
            CanonicalIcoPath = icoPath,
            FrameSizes = "256",
            FileSizeBytes = new FileInfo(icoPath).Length,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        });
        await _storage.SetLastAppliedRenderKeyAsync(renderKey);

        // 1. Target is a file, not a directory
        string filePath = Path.Combine(_testRoot, "regular_file.txt");
        await File.WriteAllTextAsync(filePath, "test content");

        var fileResult = await ApplyLastService.ApplyLastAsync(filePath, _storage, _customizer);
        Assert.False(fileResult.IsSuccess);
        Assert.Equal(ErrorCode.ItemIsFileNotFolder, fileResult.ErrorCode);

        // 2. Target does not exist
        string nonExistentPath = Path.Combine(_testRoot, "NonExistentDirectory_12345");
        var nonExistentResult = await ApplyLastService.ApplyLastAsync(nonExistentPath, _storage, _customizer);
        Assert.False(nonExistentResult.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, nonExistentResult.ErrorCode);

        // 3. Null or whitespace path
        var emptyResult = await ApplyLastService.ApplyLastAsync("", _storage, _customizer);
        Assert.False(emptyResult.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, emptyResult.ErrorCode);
    }

    [Fact]
    public async Task ApplyLastSameFolderConcurrentActivationsSerializeSafely()
    {
        string hexColor = "#881798";
        var recipe = FolderTemplateRenderer.CreateRecipe(hexColor);
        string renderKey = recipe.ComputeRenderKey();
        string icoPath = Path.Combine(_testRoot, "concurrent_test.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, hexColor, icoPath);
        Assert.True(renderResult.IsSuccess);

        await _storage.SaveRenderCacheEntryAsync(new RenderCacheEntry
        {
            RenderKey = renderKey,
            RecipeVersion = 1,
            RecipeJson = recipe.ToJson(),
            SourceAssetHash = recipe.SourceAssetHash,
            CanonicalIcoPath = icoPath,
            FrameSizes = "256",
            FileSizeBytes = new FileInfo(icoPath).Length,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        });
        await _storage.SetLastAppliedRenderKeyAsync(renderKey);

        string concurrentFolder = Path.Combine(_testRoot, "FolderConcurrent");
        Directory.CreateDirectory(concurrentFolder);

        // 5 concurrent ApplyLastAsync requests on the same folder
        var tasks = new Task<OperationResult>[5];
        for (int i = 0; i < 5; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                return await ApplyLastService.ApplyLastAsync(concurrentFolder, _storage, _customizer);
            });
        }

        var results = await Task.WhenAll(tasks);

        // At least one or all must succeed, and no exceptions thrown
        int successCount = 0;
        foreach (var r in results)
        {
            if (r.IsSuccess) successCount++;
        }

        Assert.True(successCount >= 1, "At least one operation must succeed.");

        // Resulting folder must have valid customization
        string finalIco = Path.Combine(concurrentFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(finalIco));
    }
}
