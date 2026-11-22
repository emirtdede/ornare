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

    private async Task EnsureStorageInitializedAsync(bool readOnly = false)
    {
        if (!_initialized)
        {
            if (readOnly)
            {
                await _storage.InitializeReadOnlyAsync();
            }
            else
            {
                await _storage.InitializeAsync();
            }
            _initialized = true;
        }
    }

    private static int OutputPlanResponse(bool isJson, OperationPlan plan)
    {
        bool isSuccess = plan.Status != PlanStatus.WouldFail;
        string code = isSuccess ? "Success" : (plan.Blockers.Count > 0 ? plan.Blockers[0].Code : "PlanFailed");
        string messageKey = isSuccess ? plan.Status.ToString() : (plan.Blockers.Count > 0 ? plan.Blockers[0].Message : "Operation would fail");

        var env = isSuccess
            ? CliEnvelope.CreateSuccess(plan.Command, plan)
            : CliEnvelope.CreateFailure(plan.Command, code, messageKey, operationId: null, data: plan);

        CliResponseFormatter.WritePlan(isJson, env);

        if (isSuccess)
        {
            return CliExitCodes.Success;
        }

        if (Enum.TryParse<ErrorCode>(code, out var ec))
        {
            return CliExitCodes.FromErrorCode(ec);
        }

        if (string.Equals(code, "InvalidArguments", StringComparison.OrdinalIgnoreCase))
        {
            return CliExitCodes.InvalidArguments;
        }

        return CliExitCodes.OperationFailed;
    }

    public async Task<int> HandleApplyAsync(string folderPath, RenderInput renderInput, bool isJson, bool isDryRun = false)
    {
        // Measures the documented PERFORMANCE.md apply latency: valid input through
        // durable state. Deliberately excludes process start, runtime init and native
        // library load, which are one-off costs and not part of the apply work.
        long applyStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        var validation = RenderInputValidator.Validate(renderInput);
        if (!validation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "apply", validation.Code, validation.ErrorMessage);
            return validation.ExitCode;
        }

        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "apply",
                    Target: folderPath ?? string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate target folder eligibility and permissions"],
                    Blockers: [new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "apply", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        if (isDryRun)
        {
            await EnsureStorageInitializedAsync(readOnly: true);

            var validated = validation.Value!;
            RenderRecipe recipe;
            string sourceFormat = "ico";
            string? sourceMetadata = null;
            string? contentHash = null;

            if (validated.Kind == RenderInputKind.Color)
            {
                sourceFormat = "color";
                sourceMetadata = $"Color: {validated.ColorHex}, Template: {validated.TemplateId}";
                recipe = FolderTemplateRenderer.CreateRecipe(validated.ColorHex!, validated.TemplateId, 1);
            }
            else if (validated.Kind == RenderInputKind.Preset)
            {
                sourceFormat = "preset";
                sourceMetadata = $"Preset: {validated.Preset}";
                recipe = FolderTemplateRenderer.CreatePresetRecipe(validated.Preset!, FolderTemplateCatalog.DefaultTemplateId, 1);
            }
            else
            {
                sourceFormat = Path.GetExtension(validated.SourcePath!).TrimStart('.').ToLowerInvariant();
                sourceMetadata = $"Source: {validated.SourcePath}";
                bool isSvg = string.Equals(Path.GetExtension(validated.SourcePath), ".svg", StringComparison.OrdinalIgnoreCase);

                if (!File.Exists(validated.SourcePath))
                {
                    var missingPlan = new OperationPlan(
                        Command: "apply",
                        Target: folderPath ?? string.Empty,
                        Status: PlanStatus.WouldFail,
                        PlannedActions: ["Validate target folder and source image"],
                        Blockers: [new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Source image file does not exist: '{validated.SourcePath}'.")],
                        IndeterminateConditions: []
                    );
                    return OutputPlanResponse(isJson, missingPlan);
                }

                byte[] sourceBytes;
                try
                {
                    sourceBytes = await File.ReadAllBytesAsync(validated.SourcePath!);
                    contentHash = _pipeline.ComputeSourceHash(sourceBytes);

                    if (isSvg)
                    {
                        var sec = SvgSecurityValidator.Validate(sourceBytes);
                        if (!sec.IsSuccess)
                        {
                            var secPlan = new OperationPlan(
                                Command: "apply",
                                Target: folderPath ?? string.Empty,
                                Status: PlanStatus.WouldFail,
                                PlannedActions: ["Validate target folder and source image security"],
                                Blockers: [new PlanBlocker(sec.ErrorCode.ToString(), sec.ErrorMessage ?? "SVG security check failed.")],
                                IndeterminateConditions: []
                            );
                            return OutputPlanResponse(isJson, secPlan);
                        }

                        if (!string.IsNullOrEmpty(validated.RecolorHex))
                        {
                            string svgStr = System.Text.Encoding.UTF8.GetString(sourceBytes);
                            var recolorCheck = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(svgStr);
                            if (!recolorCheck.IsSuccess)
                            {
                                var recolorPlan = new OperationPlan(
                                    Command: "apply",
                                    Target: folderPath ?? string.Empty,
                                    Status: PlanStatus.WouldFail,
                                    PlannedActions: ["Validate target folder and SVG recolor themeability"],
                                    Blockers: [new PlanBlocker(recolorCheck.ErrorCode.ToString(), recolorCheck.ErrorMessage ?? "SVG is not single-color themeable.")],
                                    IndeterminateConditions: []
                                );
                                return OutputPlanResponse(isJson, recolorPlan);
                            }
                        }
                    }
                    else
                    {
                        if (!_pipeline.ValidateImageFormat(validated.SourcePath))
                        {
                            var formatPlan = new OperationPlan(
                                Command: "apply",
                                Target: folderPath ?? string.Empty,
                                Status: PlanStatus.WouldFail,
                                PlannedActions: ["Validate source image format"],
                                Blockers: [new PlanBlocker(ErrorCode.UnsupportedImageFormat.ToString(), $"Image format '{Path.GetExtension(validated.SourcePath)}' is not supported.")],
                                IndeterminateConditions: []
                            );
                            return OutputPlanResponse(isJson, formatPlan);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var readPlan = new OperationPlan(
                        Command: "apply",
                        Target: folderPath ?? string.Empty,
                        Status: PlanStatus.WouldFail,
                        PlannedActions: ["Read source image"],
                        Blockers: [new PlanBlocker(ErrorCode.ImageCorrupted.ToString(), $"Failed to read source image: {ex.Message}")],
                        IndeterminateConditions: []
                    );
                    return OutputPlanResponse(isJson, readPlan);
                }

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
            }

            var plan = await OperationPlanningService.PlanApplyAsync(
                folderPath,
                sourceFormat,
                contentHash,
                sourceMetadata,
                recipe,
                _storage,
                _customizer);

            return OutputPlanResponse(isJson, plan);
        }

        await EnsureStorageInitializedAsync(readOnly: false);

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
                    ContentHash = applyResult.Value.ContentHash,
                    DurationMs = System.Diagnostics.Stopwatch.GetElapsedTime(applyStartTimestamp).TotalMilliseconds
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup is best-effort; a leftover staging ICO is collected by the
                // next staging pass and must not fail the command after a successful apply.
                Console.Error.WriteLine($"Warning: could not delete staging file '{tempIco}': {ex.Message}");
            }
        }
    }

    public async Task<int> HandleApplyLastAsync(string folderPath, bool isJson, bool isDryRun = false)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "apply-last",
                    Target: folderPath ?? string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate target folder eligibility and permissions"],
                    Blockers: [new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "apply-last", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        if (isDryRun)
        {
            await EnsureStorageInitializedAsync(readOnly: true);
            var plan = await OperationPlanningService.PlanApplyLastAsync(folderPath, _storage, _customizer);
            return OutputPlanResponse(isJson, plan);
        }

        await EnsureStorageInitializedAsync(readOnly: false);

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

    public async Task<int> HandleResetAsync(string folderPath, bool isJson, bool isDryRun = false)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "reset",
                    Target: folderPath ?? string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate target folder eligibility and permissions"],
                    Blockers: [new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "reset", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        if (isDryRun)
        {
            await EnsureStorageInitializedAsync(readOnly: true);
            var plan = await OperationPlanningService.PlanResetAsync(folderPath, _storage, _customizer);
            return OutputPlanResponse(isJson, plan);
        }

        await EnsureStorageInitializedAsync(readOnly: false);

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

    public async Task<int> HandleUndoAsync(string folderPath, bool isJson, bool isDryRun = false)
    {
        var folderValidation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!folderValidation.IsSuccess)
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "undo",
                    Target: folderPath ?? string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate target folder eligibility and permissions"],
                    Blockers: [new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "undo", folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder.");
            return CliExitCodes.FromErrorCode(folderValidation.ErrorCode);
        }

        if (isDryRun)
        {
            await EnsureStorageInitializedAsync(readOnly: true);
            var plan = await OperationPlanningService.PlanUndoAsync(folderPath, _storage, _customizer);
            return OutputPlanResponse(isJson, plan);
        }

        await EnsureStorageInitializedAsync(readOnly: false);

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

        await EnsureStorageInitializedAsync(readOnly: true);

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

    public async Task<int> HandleExportIcoAsync(string outputPath, RenderInput renderInput, bool isJson, bool isDryRun = false)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "export-ico",
                    Target: string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate export output path and source image"],
                    Blockers: [new PlanBlocker("InvalidArguments", "Output path cannot be empty.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "export-ico", "InvalidArguments", "Output path cannot be empty.");
            return CliExitCodes.InvalidArguments;
        }

        var validation = RenderInputValidator.Validate(renderInput);
        if (!validation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-ico", validation.Code, validation.ErrorMessage);
            return validation.ExitCode;
        }

        if (isDryRun)
        {
            await EnsureStorageInitializedAsync(readOnly: true);

            var validated = validation.Value!;
            RenderRecipe recipe;
            string sourceDesc;

            if (validated.Kind == RenderInputKind.Color)
            {
                sourceDesc = $"Color: {validated.ColorHex}, Template: {validated.TemplateId}";
                recipe = FolderTemplateRenderer.CreateRecipe(validated.ColorHex!, validated.TemplateId, 1);
            }
            else if (validated.Kind == RenderInputKind.Preset)
            {
                sourceDesc = $"Preset: {validated.Preset}";
                recipe = FolderTemplateRenderer.CreatePresetRecipe(validated.Preset!, FolderTemplateCatalog.DefaultTemplateId, 1);
            }
            else
            {
                sourceDesc = $"Source: {validated.SourcePath}";
                bool isSvg = string.Equals(Path.GetExtension(validated.SourcePath), ".svg", StringComparison.OrdinalIgnoreCase);

                if (!File.Exists(validated.SourcePath))
                {
                    var missingPlan = new OperationPlan(
                        Command: "export-ico",
                        Target: outputPath ?? string.Empty,
                        Status: PlanStatus.WouldFail,
                        PlannedActions: ["Validate export output path and source image"],
                        Blockers: [new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Source image file does not exist: '{validated.SourcePath}'.")],
                        IndeterminateConditions: []
                    );
                    return OutputPlanResponse(isJson, missingPlan);
                }

                byte[] sourceBytes;
                try
                {
                    sourceBytes = await File.ReadAllBytesAsync(validated.SourcePath!);
                    string contentHash = _pipeline.ComputeSourceHash(sourceBytes);

                    if (isSvg)
                    {
                        var sec = SvgSecurityValidator.Validate(sourceBytes);
                        if (!sec.IsSuccess)
                        {
                            var secPlan = new OperationPlan(
                                Command: "export-ico",
                                Target: outputPath ?? string.Empty,
                                Status: PlanStatus.WouldFail,
                                PlannedActions: ["Validate SVG security"],
                                Blockers: [new PlanBlocker(sec.ErrorCode.ToString(), sec.ErrorMessage ?? "SVG security check failed.")],
                                IndeterminateConditions: []
                            );
                            return OutputPlanResponse(isJson, secPlan);
                        }
                    }
                    else
                    {
                        if (!_pipeline.ValidateImageFormat(validated.SourcePath))
                        {
                            var formatPlan = new OperationPlan(
                                Command: "export-ico",
                                Target: outputPath ?? string.Empty,
                                Status: PlanStatus.WouldFail,
                                PlannedActions: ["Validate source image format"],
                                Blockers: [new PlanBlocker(ErrorCode.UnsupportedImageFormat.ToString(), $"Image format '{Path.GetExtension(validated.SourcePath)}' is not supported.")],
                                IndeterminateConditions: []
                            );
                            return OutputPlanResponse(isJson, formatPlan);
                        }
                    }

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
                }
                catch (Exception ex)
                {
                    var readPlan = new OperationPlan(
                        Command: "export-ico",
                        Target: outputPath ?? string.Empty,
                        Status: PlanStatus.WouldFail,
                        PlannedActions: ["Read source image"],
                        Blockers: [new PlanBlocker(ErrorCode.ImageCorrupted.ToString(), $"Failed to read source image: {ex.Message}")],
                        IndeterminateConditions: []
                    );
                    return OutputPlanResponse(isJson, readPlan);
                }
            }

            var plan = await OperationPlanningService.PlanExportIcoAsync(outputPath, sourceDesc, recipe, _storage);
            return OutputPlanResponse(isJson, plan);
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

        await EnsureStorageInitializedAsync(readOnly: false);

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

    public async Task<int> HandleExportPngAsync(string outputPath, int size, RenderInput renderInput, bool isJson, bool isDryRun = false)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "export-png",
                    Target: string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate export output path and source image"],
                    Blockers: [new PlanBlocker("InvalidArguments", "Output path cannot be empty.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "export-png", "InvalidArguments", "Output path cannot be empty.");
            return CliExitCodes.InvalidArguments;
        }

        if (size != 256 && size != 512 && size != 1024)
        {
            if (isDryRun)
            {
                var failPlan = new OperationPlan(
                    Command: "export-png",
                    Target: outputPath ?? string.Empty,
                    Status: PlanStatus.WouldFail,
                    PlannedActions: ["Validate export dimensions"],
                    Blockers: [new PlanBlocker("InvalidArguments", $"Invalid PNG export size '{size}'. Allowed sizes are 256, 512, 1024.")],
                    IndeterminateConditions: []
                );
                return OutputPlanResponse(isJson, failPlan);
            }

            CliResponseFormatter.WriteGeneralError(isJson, "export-png", "InvalidArguments", $"Invalid PNG export size '{size}'. Allowed sizes are 256, 512, 1024.");
            return CliExitCodes.InvalidArguments;
        }

        var validation = RenderInputValidator.Validate(renderInput);
        if (!validation.IsSuccess)
        {
            CliResponseFormatter.WriteGeneralError(isJson, "export-png", validation.Code, validation.ErrorMessage);
            return validation.ExitCode;
        }

        if (isDryRun)
        {
            var validated = validation.Value!;
            RenderRecipe recipe;
            string sourceDesc;

            if (validated.Kind == RenderInputKind.Color)
            {
                sourceDesc = $"Color: {validated.ColorHex}, Template: {validated.TemplateId}";
                recipe = FolderTemplateRenderer.CreateRecipe(validated.ColorHex!, validated.TemplateId, 1);
            }
            else if (validated.Kind == RenderInputKind.Preset)
            {
                sourceDesc = $"Preset: {validated.Preset}";
                recipe = FolderTemplateRenderer.CreatePresetRecipe(validated.Preset!, FolderTemplateCatalog.DefaultTemplateId, 1);
            }
            else
            {
                sourceDesc = $"Source: {validated.SourcePath}";
                bool isSvg = string.Equals(Path.GetExtension(validated.SourcePath), ".svg", StringComparison.OrdinalIgnoreCase);

                if (!File.Exists(validated.SourcePath))
                {
                    var missingPlan = new OperationPlan(
                        Command: "export-png",
                        Target: outputPath ?? string.Empty,
                        Status: PlanStatus.WouldFail,
                        PlannedActions: ["Validate export output path and source image"],
                        Blockers: [new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Source image file does not exist: '{validated.SourcePath}'.")],
                        IndeterminateConditions: []
                    );
                    return OutputPlanResponse(isJson, missingPlan);
                }

                byte[] sourceBytes;
                try
                {
                    sourceBytes = await File.ReadAllBytesAsync(validated.SourcePath!);
                    string contentHash = _pipeline.ComputeSourceHash(sourceBytes);

                    if (isSvg)
                    {
                        var sec = SvgSecurityValidator.Validate(sourceBytes);
                        if (!sec.IsSuccess)
                        {
                            var secPlan = new OperationPlan(
                                Command: "export-png",
                                Target: outputPath ?? string.Empty,
                                Status: PlanStatus.WouldFail,
                                PlannedActions: ["Validate SVG security"],
                                Blockers: [new PlanBlocker(sec.ErrorCode.ToString(), sec.ErrorMessage ?? "SVG security check failed.")],
                                IndeterminateConditions: []
                            );
                            return OutputPlanResponse(isJson, secPlan);
                        }
                    }
                    else
                    {
                        if (!_pipeline.ValidateImageFormat(validated.SourcePath))
                        {
                            var formatPlan = new OperationPlan(
                                Command: "export-png",
                                Target: outputPath ?? string.Empty,
                                Status: PlanStatus.WouldFail,
                                PlannedActions: ["Validate source image format"],
                                Blockers: [new PlanBlocker(ErrorCode.UnsupportedImageFormat.ToString(), $"Image format '{Path.GetExtension(validated.SourcePath)}' is not supported.")],
                                IndeterminateConditions: []
                            );
                            return OutputPlanResponse(isJson, formatPlan);
                        }
                    }

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
                catch (Exception ex)
                {
                    var readPlan = new OperationPlan(
                        Command: "export-png",
                        Target: outputPath ?? string.Empty,
                        Status: PlanStatus.WouldFail,
                        PlannedActions: ["Read source image"],
                        Blockers: [new PlanBlocker(ErrorCode.ImageCorrupted.ToString(), $"Failed to read source image: {ex.Message}")],
                        IndeterminateConditions: []
                    );
                    return OutputPlanResponse(isJson, readPlan);
                }
            }

            var plan = await OperationPlanningService.PlanExportPngAsync(outputPath, size, sourceDesc, recipe);
            return OutputPlanResponse(isJson, plan);
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

        await EnsureStorageInitializedAsync(readOnly: false);

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
                string libraryIco = OrnareConstants.LibraryIcoPath(entry.SourceAssetHash);
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
                    string libraryIco = OrnareConstants.LibraryIcoPath(recipe.SourceAssetHash);
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
        catch (Exception ex)
        {
            // Recovery is best-effort: a failed rebuild surfaces as "no asset" to the
            // caller, but the cause must still be visible in diagnostics.
            Console.Error.WriteLine($"Warning: cache asset rebuild failed: {ex.Message}");
        }
        return null;
    }

    public ValueTask DisposeAsync()
    {
        _storage.Dispose();
        return ValueTask.CompletedTask;
    }
}
