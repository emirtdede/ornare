using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Windows;

namespace Ornare.Cli;

/// <summary>
/// Execution handlers for Ornare CLI subcommands (F24).
/// Delegates directly to shared Core operations and existing Windows/Imaging/Storage services.
/// </summary>
public sealed class CliHandlers : IAsyncDisposable
{
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;
    private readonly ImagePipeline _pipeline;
    private bool _initialized;

    public CliHandlers()
    {
        _storage = new SqliteStorageService();
        _customizer = new FolderCustomizer();
        _pipeline = new ImagePipeline();
    }

    private async Task EnsureStorageInitializedAsync()
    {
        if (!_initialized)
        {
            await _storage.InitializeAsync();
            _initialized = true;
        }
    }

    public async Task<int> HandleApplyAsync(string folderPath, RenderInput renderInput, bool isJson)
    {
        var validation = RenderInputValidator.Validate(renderInput);
        if (!validation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "apply", validation.Code, validation.ErrorMessage);
            return validation.ExitCode;
        }

        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "apply", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        await EnsureStorageInitializedAsync();

        string tempDir = Path.Combine(Path.GetTempPath(), "Ornare", "CliStaging");
        Directory.CreateDirectory(tempDir);
        string tempIco = Path.Combine(tempDir, $"{Guid.NewGuid():N}.ico");

        try
        {
            var validated = validation.Value!;
            OperationResult<NormalizedImageResult> renderResult;
            RenderRecipe recipe;
            string sourceFormat = "ico";

            if (validated.Kind == RenderInputKind.Color)
            {
                sourceFormat = "color";
                recipe = FolderTemplateRenderer.CreateRecipe(validated.ColorHex!, validated.TemplateId, 1);
                var descriptor = FolderTemplateCatalog.GetTemplate(validated.TemplateId, 1);
                byte[] templateBytes = descriptor.GetBytes();
                renderResult = _pipeline.ProcessRecipe(
                    recipe,
                    templateBytes,
                    tempIco,
                    cacheLookup: key => _storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                    cacheStore: cacheEntry => _storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());
            }
            else if (validated.Kind == RenderInputKind.Preset)
            {
                sourceFormat = "preset";
                recipe = FolderTemplateRenderer.CreatePresetRecipe(validated.Preset!, FolderTemplateCatalog.DefaultTemplateId, 1);
                var descriptor = FolderTemplateCatalog.GetTemplate(FolderTemplateCatalog.DefaultTemplateId, 1);
                byte[] templateBytes = descriptor.GetBytes();
                renderResult = _pipeline.ProcessRecipe(
                    recipe,
                    templateBytes,
                    tempIco,
                    cacheLookup: key => _storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                    cacheStore: cacheEntry => _storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());
            }
            else
            {
                sourceFormat = Path.GetExtension(validated.SourcePath!).TrimStart('.').ToLowerInvariant();
                byte[] sourceBytes = await File.ReadAllBytesAsync(validated.SourcePath!);
                string contentHash = _pipeline.ComputeSourceHash(sourceBytes);
                bool isSvg = string.Equals(Path.GetExtension(validated.SourcePath), ".svg", StringComparison.OrdinalIgnoreCase);

                recipe = new RenderRecipe
                {
                    SourceAssetHash = contentHash,
                    PlacementMode = (int)validated.Mode,
                    PaddingPercent = validated.Padding,
                    BackgroundMode = (int)validated.BackgroundMode,
                    BackgroundColor = validated.BackgroundMode == BackgroundMode.SolidColor && !string.IsNullOrEmpty(validated.BackgroundColorHex)
                        ? BackgroundSpec.NormalizeHex(validated.BackgroundColorHex)
                        : string.Empty,
                    SvgRecolorMode = (isSvg && !string.IsNullOrEmpty(validated.RecolorHex)) ? 2 : 0,
                    SvgRecolorValue = (isSvg && !string.IsNullOrEmpty(validated.RecolorHex)) ? validated.RecolorHex : string.Empty,
                    TargetFormat = "ico",
                    TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
                };

                renderResult = _pipeline.ProcessRecipe(
                    recipe,
                    sourceBytes,
                    tempIco,
                    cacheLookup: key => _storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                    cacheStore: cacheEntry => _storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());
            }

            if (!renderResult.IsSuccess || renderResult.Value == null)
            {
                int exitCode = CliExitCodes.FromErrorCode(renderResult.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "apply", renderResult.ErrorCode.ToString(), renderResult.ErrorMessage ?? "Icon rendering failed.");
                return exitCode;
            }

            var applyResult = await ApplyService.ApplyAsync(
                folderPath,
                tempIco,
                renderResult.Value.ContentHash,
                renderResult.Value.RenderKey,
                recipe,
                _storage,
                _customizer,
                sourceFormat);

            if (!applyResult.IsSuccess || applyResult.Value == null)
            {
                int exitCode = CliExitCodes.FromErrorCode(applyResult.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "apply", applyResult.ErrorCode.ToString(), applyResult.ErrorMessage ?? "Applying icon failed.");
                return exitCode;
            }

            var env = CliEnvelope.CreateSuccess(
                "apply",
                new ApplyData
                {
                    TargetFolder = Path.GetFullPath(folderPath),
                    CanonicalIcoPath = applyResult.Value.CanonicalIcoPath,
                    RenderKey = applyResult.Value.RenderKey,
                    ContentHash = applyResult.Value.ContentHash
                },
                operationId: applyResult.Value.OperationId.ToString());

            CliResponseFormatter.WriteApply(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "apply", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
        finally
        {
            try
            {
                if (File.Exists(tempIco)) File.Delete(tempIco);
            }
            catch { }
        }
    }

    public async Task<int> HandleApplyLastAsync(string folderPath, bool isJson)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "apply-last", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        await EnsureStorageInitializedAsync();

        try
        {
            var result = await ApplyLastService.ApplyLastAsync(
                folderPath,
                _storage,
                _customizer,
                rebuildIcoFunc: entry => RebuildIcoFromRecipeAsync(entry, _storage, _pipeline));

            if (!result.IsSuccess)
            {
                int exitCode = CliExitCodes.FromErrorCode(result.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "apply-last", result.ErrorCode.ToString(), result.ErrorMessage ?? "Apply-last failed.");
                return exitCode;
            }

            var env = CliEnvelope.CreateSuccess(
                "apply-last",
                new ApplyLastData
                {
                    TargetFolder = Path.GetFullPath(folderPath)
                });

            CliResponseFormatter.WriteApplyLast(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "apply-last", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
    }

    public async Task<int> HandleResetAsync(string folderPath, bool isJson)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "reset", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        await EnsureStorageInitializedAsync();

        try
        {
            var result = await ResetService.ResetAsync(folderPath, _storage, _customizer);
            if (!result.IsSuccess)
            {
                int exitCode = CliExitCodes.FromErrorCode(result.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "reset", result.ErrorCode.ToString(), result.ErrorMessage ?? "Reset failed.");
                return exitCode;
            }

            var env = CliEnvelope.CreateSuccess(
                "reset",
                new ResetData
                {
                    TargetFolder = Path.GetFullPath(folderPath)
                });

            CliResponseFormatter.WriteReset(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "reset", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
    }

    public async Task<int> HandleUndoAsync(string folderPath, bool isJson)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "undo", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        await EnsureStorageInitializedAsync();

        try
        {
            var result = await UndoService.UndoAsync(folderPath, _storage, _customizer);
            if (!result.IsSuccess)
            {
                int exitCode = CliExitCodes.FromErrorCode(result.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "undo", result.ErrorCode.ToString(), result.ErrorMessage ?? "Undo failed.");
                return exitCode;
            }

            var env = CliEnvelope.CreateSuccess(
                "undo",
                new UndoData
                {
                    TargetFolder = Path.GetFullPath(folderPath)
                });

            CliResponseFormatter.WriteUndo(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "undo", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
    }

    public async Task<int> HandleInspectAsync(string folderPath, bool isJson)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "inspect", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        await EnsureStorageInitializedAsync();

        try
        {
            var result = await InspectService.InspectAsync(folderPath, _storage, _customizer);
            if (!result.IsSuccess || result.Value == null)
            {
                int exitCode = CliExitCodes.FromErrorCode(result.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "inspect", result.ErrorCode.ToString(), result.ErrorMessage ?? "Inspect failed.");
                return exitCode;
            }

            var insp = result.Value;
            var env = CliEnvelope.CreateSuccess(
                "inspect",
                new InspectData
                {
                    TargetFolder = insp.TargetFolder,
                    IsCustomized = insp.IsCustomized,
                    State = insp.State.ToString(),
                    DesktopIniExists = insp.DesktopIniExists,
                    IconResource = insp.IconResource,
                    IconResourceIndex = insp.IconResourceIndex,
                    HasBaseline = insp.HasBaseline,
                    HasAssignment = insp.HasAssignment,
                    CanonicalIcoPath = insp.CanonicalIcoPath,
                    VolumeSerialNumber = insp.VolumeSerialNumber,
                    FileId = insp.FileId
                });

            CliResponseFormatter.WriteInspect(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "inspect", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
    }

    public async Task<int> HandleExportIcoAsync(string outputPath, RenderInput renderInput, bool isJson)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-ico", "InvalidArguments", "Output path cannot be empty.");
            return CliExitCodes.InvalidArguments;
        }

        var validation = RenderInputValidator.Validate(renderInput);
        if (!validation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-ico", validation.Code, validation.ErrorMessage);
            return validation.ExitCode;
        }

        string fullOutputPath;
        try
        {
            fullOutputPath = Path.GetFullPath(outputPath);
            string? outDir = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-ico", "StorageError", $"Invalid output path: {ex.Message}");
            return CliExitCodes.StorageError;
        }

        await EnsureStorageInitializedAsync();

        try
        {
            var validated = validation.Value!;
            OperationResult<NormalizedImageResult> renderResult;

            if (validated.Kind == RenderInputKind.Color)
            {
                renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
                    _pipeline,
                    validated.ColorHex!,
                    fullOutputPath,
                    validated.TemplateId,
                    1,
                    cacheLookup: key => _storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                    cacheStore: cacheEntry => _storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());
            }
            else if (validated.Kind == RenderInputKind.Preset)
            {
                renderResult = FolderTemplateRenderer.RenderPresetIco(
                    _pipeline,
                    validated.Preset!,
                    fullOutputPath,
                    FolderTemplateCatalog.DefaultTemplateId,
                    1,
                    cacheLookup: key => _storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                    cacheStore: cacheEntry => _storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());
            }
            else
            {
                byte[] sourceBytes = await File.ReadAllBytesAsync(validated.SourcePath!);
                string contentHash = _pipeline.ComputeSourceHash(sourceBytes);
                bool isSvg = string.Equals(Path.GetExtension(validated.SourcePath), ".svg", StringComparison.OrdinalIgnoreCase);

                var recipe = new RenderRecipe
                {
                    SourceAssetHash = contentHash,
                    PlacementMode = (int)validated.Mode,
                    PaddingPercent = validated.Padding,
                    BackgroundMode = (int)validated.BackgroundMode,
                    BackgroundColor = validated.BackgroundMode == BackgroundMode.SolidColor && !string.IsNullOrEmpty(validated.BackgroundColorHex)
                        ? BackgroundSpec.NormalizeHex(validated.BackgroundColorHex)
                        : string.Empty,
                    SvgRecolorMode = (isSvg && !string.IsNullOrEmpty(validated.RecolorHex)) ? 2 : 0,
                    SvgRecolorValue = (isSvg && !string.IsNullOrEmpty(validated.RecolorHex)) ? validated.RecolorHex : string.Empty,
                    TargetFormat = "ico",
                    TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
                };

                renderResult = _pipeline.ProcessRecipe(
                    recipe,
                    sourceBytes,
                    fullOutputPath,
                    cacheLookup: key => _storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                    cacheStore: cacheEntry => _storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());
            }

            if (!renderResult.IsSuccess || !File.Exists(fullOutputPath))
            {
                int exitCode = CliExitCodes.FromErrorCode(renderResult.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "export-ico", renderResult.ErrorCode.ToString(), renderResult.ErrorMessage ?? "Export ICO failed.");
                return exitCode;
            }

            FileInfo fi = new(fullOutputPath);
            var env = CliEnvelope.CreateSuccess(
                "export-ico",
                new ExportIcoData
                {
                    OutputPath = fullOutputPath,
                    RenderKey = renderResult.Value?.RenderKey,
                    FileSizeBytes = fi.Length,
                    FrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
                });

            CliResponseFormatter.WriteExportIco(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-ico", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
    }

    public async Task<int> HandleExportPngAsync(string outputPath, int size, RenderInput renderInput, bool isJson)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-png", "InvalidArguments", "Output path cannot be empty.");
            return CliExitCodes.InvalidArguments;
        }

        if (size != 256 && size != 512 && size != 1024)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-png", "InvalidArguments", $"Invalid PNG export size '{size}'. Allowed sizes are 256, 512, 1024.");
            return CliExitCodes.InvalidArguments;
        }

        var validation = RenderInputValidator.Validate(renderInput);
        if (!validation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-png", validation.Code, validation.ErrorMessage);
            return validation.ExitCode;
        }

        string fullOutputPath;
        try
        {
            fullOutputPath = Path.GetFullPath(outputPath);
            string? outDir = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-png", "StorageError", $"Invalid output path: {ex.Message}");
            return CliExitCodes.StorageError;
        }

        await EnsureStorageInitializedAsync();

        try
        {
            var validated = validation.Value!;
            RenderRecipe recipe;
            byte[]? sourceBytes = null;

            if (validated.Kind == RenderInputKind.Color)
            {
                recipe = FolderTemplateRenderer.CreateRecipe(validated.ColorHex!, validated.TemplateId, 1);
            }
            else if (validated.Kind == RenderInputKind.Preset)
            {
                recipe = FolderTemplateRenderer.CreatePresetRecipe(validated.Preset!, FolderTemplateCatalog.DefaultTemplateId, 1);
            }
            else
            {
                sourceBytes = await File.ReadAllBytesAsync(validated.SourcePath!);
                string contentHash = _pipeline.ComputeSourceHash(sourceBytes);
                bool isSvg = string.Equals(Path.GetExtension(validated.SourcePath), ".svg", StringComparison.OrdinalIgnoreCase);

                recipe = new RenderRecipe
                {
                    SourceAssetHash = contentHash,
                    PlacementMode = (int)validated.Mode,
                    PaddingPercent = validated.Padding,
                    BackgroundMode = (int)validated.BackgroundMode,
                    BackgroundColor = validated.BackgroundMode == BackgroundMode.SolidColor && !string.IsNullOrEmpty(validated.BackgroundColorHex)
                        ? BackgroundSpec.NormalizeHex(validated.BackgroundColorHex)
                        : string.Empty,
                    SvgRecolorMode = (isSvg && !string.IsNullOrEmpty(validated.RecolorHex)) ? 2 : 0,
                    SvgRecolorValue = (isSvg && !string.IsNullOrEmpty(validated.RecolorHex)) ? validated.RecolorHex : string.Empty,
                    TargetFormat = "png",
                    TargetFrameSizes = [size]
                };
            }

            string renderKey = recipe.ComputeRenderKey();
            var result = PngExportService.RenderPng(recipe, sourceBytes, size);
            if (!result.IsSuccess || result.Value == null)
            {
                int exitCode = CliExitCodes.FromErrorCode(result.ErrorCode);
                CliResponseFormatter.WriteGeneralError(isJson, "export-png", result.ErrorCode.ToString(), result.ErrorMessage ?? "PNG rendering failed.");
                return exitCode;
            }

            byte[] pngBytes = result.Value;

            await File.WriteAllBytesAsync(fullOutputPath, pngBytes);

            FileInfo fi = new(fullOutputPath);
            var env = CliEnvelope.CreateSuccess(
                "export-png",
                new ExportPngData
                {
                    OutputPath = fullOutputPath,
                    RenderKey = renderKey,
                    Width = size,
                    Height = size,
                    FileSizeBytes = fi.Length
                });

            CliResponseFormatter.WriteExportPng(isJson, env);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-png", "UnexpectedError", ex.Message);
            return CliExitCodes.UnexpectedError;
        }
    }

    private static async Task<string?> RebuildIcoFromRecipeAsync(RenderCacheEntry entry, SqliteStorageService storage, ImagePipeline pipeline)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.RecipeJson))
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string libraryIco = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library", $"{entry.SourceAssetHash}.ico");
                if (File.Exists(libraryIco))
                {
                    string? outDir = Path.GetDirectoryName(entry.CanonicalIcoPath);
                    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                    File.Copy(libraryIco, entry.CanonicalIcoPath, overwrite: true);
                    return entry.CanonicalIcoPath;
                }
                return null;
            }

            var recipe = RenderRecipe.FromJson(entry.RecipeJson);
            if (recipe == null) return null;

            byte[]? sourceBytes = null;
            if (!string.IsNullOrEmpty(recipe.TemplateId))
            {
                var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
                sourceBytes = descriptor.GetBytes();
            }
            else if (!string.IsNullOrEmpty(recipe.SourceAssetHash))
            {
                var asset = await storage.GetAssetByHashAsync(recipe.SourceAssetHash);
                if (asset != null && File.Exists(asset.CanonicalIcoPath))
                {
                    sourceBytes = await File.ReadAllBytesAsync(asset.CanonicalIcoPath);
                }
                else
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string libraryIco = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library", $"{recipe.SourceAssetHash}.ico");
                    if (File.Exists(libraryIco))
                    {
                        sourceBytes = await File.ReadAllBytesAsync(libraryIco);
                    }
                }
            }

            if (sourceBytes == null || sourceBytes.Length == 0) return null;

            string? dir = Path.GetDirectoryName(entry.CanonicalIcoPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var renderResult = pipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                entry.CanonicalIcoPath,
                cacheLookup: key => storage.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                cacheStore: cacheEntry => storage.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());

            if (renderResult.IsSuccess && File.Exists(entry.CanonicalIcoPath))
            {
                return entry.CanonicalIcoPath;
            }
        }
        catch { }
        return null;
    }

    public ValueTask DisposeAsync()
    {
        _storage.Dispose();
        return ValueTask.CompletedTask;
    }
}
