namespace Ornare.App;

using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Ornare.Core;
using Ornare.Storage;
using Ornare.Windows;

/// <summary>
/// Commit, undo and reset: the only place the quick panel mutates the filesystem.
/// <para>
/// These three flows share one contract - acquire the commit flag, write through the single
/// folder-customizer funnel, then reconcile the stored assignment - so they sit together instead
/// of being interleaved with picker and catalogue handlers.
/// </para>
/// </summary>
public sealed partial class QuickPanelWindow
{
    private async Task CommitStagedIconAsync()
    {
        // Reentrancy is guarded by a dedicated commit flag, not by _isProcessing.
        // The apply flow legitimately sets _isProcessing=true while rendering, so
        // keying this guard on _isProcessing silently skipped every commit.
        if (_isCommitting || string.IsNullOrWhiteSpace(_stagedTempIcoPath) || !File.Exists(_stagedTempIcoPath) || _stagedProcessResult == null)
        {
            if (!_isCommitting && _stagedProcessResult == null)
            {
                ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, QuickPanelErrorCopy.Message(AppResources.GetString("ErrorIconGenerationMessage", _currentLanguage)));
            }
            return;
        }

        // Snapshot the operation identity. SetTargetFolderAsync may be called from a new
        // shell activation while this commit is running; every filesystem and storage
        // write below must address the captured target, never the mutable field.
        string targetFolder = _currentFolderPath;
        Guid epochId = _currentOwnershipEpochId;
        FolderBaseline? baseline = _currentBaseline;
        Guid operationId = _stagedOperationId;
        string stagedIcoPath = _stagedTempIcoPath;
        var stagedResult = _stagedProcessResult;
        string? stagedColorHex = _stagedCustomColorHex;

        _isCommitting = true;
        bool filesystemCommitted = false;
        try
        {
            using var lockHandle = await PerTargetSyncLock.TryAcquireAsync(targetFolder, TimeSpan.FromSeconds(5));
            if (lockHandle == null)
            {
                ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Message(AppResources.GetString("ErrorFolderLockedMessage", _currentLanguage)));
                return;
            }

            // The journal must reference the durable library ICO, not the staging
            // temp file: the temp file is deleted by CleanupStagedFiles() after a
            // successful apply, so crash-recovery reconciliation would otherwise
            // resolve TargetAssetPath to a path that no longer exists.
            string libraryIcoPath = OrnareConstants.LibraryIcoPath(stagedResult.ContentHash);

            // Journal step: Prepared & AssetGenerated
            JournalEntry journal = new()
            {
                OperationId = operationId,
                Kind = OperationKind.Apply,
                TargetFolderPath = targetFolder,
                State = JournalState.Prepared,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                TargetAssetPath = libraryIcoPath
            };
            await _storageService.RecordJournalEntryAsync(journal);
            await _storageService.UpdateJournalStateAsync(operationId, JournalState.AssetGenerated);

            // 1. Applying icon to folder
            SetProcessingStage(ProcessingStage.ApplyingIcon);

            if (baseline == null)
            {
                FolderIconSemantics prior = DesktopIniParser.ReadSemantics(targetFolder);
                epochId = Guid.NewGuid();
                baseline = new FolderBaseline
                {
                    FolderAssignmentId = Guid.NewGuid(),
                    OwnershipEpochId = epochId,
                    PriorSemantics = prior,
                    CapturedAt = DateTimeOffset.UtcNow
                };
            }

            // Journal transitions raised by the synchronous ApplyIcon callback must be
            // persisted in order. Fire-and-forget writes could land after the final
            // JournalState.Completed and regress a successfully applied folder, so the
            // states are buffered and flushed sequentially once ApplyIcon returns.
            var journalStages = new System.Collections.Concurrent.ConcurrentQueue<JournalState>();
            var applyResult = await Task.Run(() => _folderCustomizer.ApplyIcon(targetFolder, stagedIcoPath, journalStages.Enqueue));

            while (journalStages.TryDequeue(out JournalState stage))
            {
                await _storageService.UpdateJournalStateAsync(operationId, stage);
            }

            if (!applyResult.IsSuccess || applyResult.Value == null)
            {
                App.Log($"ApplyIcon failed: {applyResult.DiagnosticDescription}");
                await _storageService.UpdateJournalStateAsync(operationId, JournalState.NeedsRepair, applyResult.DiagnosticDescription);
                ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Diagnostic(applyResult.DiagnosticDescription));
                return;
            }

            // 2. Refreshing Explorer
            SetProcessingStage(ProcessingStage.RefreshingExplorer);

            // From this point the icon is committed on disk. Any later failure must be
            // reported as a repairable partial success, never as a clean failure
            // (INV-FS-006): reporting failure would invite a retry that re-applies the
            // icon with a fresh epoch and duplicates assignment/history rows.
            filesystemCommitted = true;

            // 3. Save assignment & baseline to database
            await _storageService.SaveFolderAssignmentAsync(
                targetFolder,
                epochId,
                applyResult.Value,
                baseline);

            // Register canonical asset in Library & History
            string canonicalIcoPath = string.Empty;
            try
            {
                canonicalIcoPath = OrnareConstants.LibraryIcoPath(stagedResult.ContentHash);
                Directory.CreateDirectory(OrnareConstants.LibraryDirectory);
                if (!File.Exists(canonicalIcoPath))
                {
                    File.Copy(stagedIcoPath, canonicalIcoPath, overwrite: true);
                }

                await _storageService.AddOrUpdateAssetAsync(
                    stagedResult.ContentHash,
                    canonicalIcoPath,
                    stagedResult.SourceFormat);

                // Ensure render_cache entry exists for deduplication and Apply Last (F18/F21)
                if (!string.IsNullOrWhiteSpace(stagedResult.RenderKey))
                {
                    var existingCache = await _storageService.GetRenderCacheEntryAsync(stagedResult.RenderKey);
                    if (existingCache == null)
                    {
                        var fi = new FileInfo(canonicalIcoPath);
                        await _storageService.SaveRenderCacheEntryAsync(new RenderCacheEntry
                        {
                            RenderKey = stagedResult.RenderKey,
                            RecipeVersion = 1,
                            RecipeJson = string.Empty,
                            SourceAssetHash = stagedResult.ContentHash,
                            CanonicalIcoPath = canonicalIcoPath,
                            FrameSizes = string.Join(",", stagedResult.FrameSizes),
                            FileSizeBytes = fi.Length,
                            CreatedAt = DateTimeOffset.UtcNow,
                            LastUsedAt = DateTimeOffset.UtcNow
                        });
                    }
                }

                await _storageService.RecordHistoryAsync(
                    targetFolder,
                    operationId,
                    null,
                    stagedResult.ContentHash,
                    "apply");

                var ntfsId = FolderIdentityService.GetNtfsIdentity(targetFolder);
                if (ntfsId.HasValue)
                {
                    await _storageService.UpdateAssignmentIdentityAsync(targetFolder, ntfsId.Value.VolumeSerialNumber, ntfsId.Value.FileId64);
                }
                else
                {
                    var (vol, fid) = FolderIdentityService.GetFolderIdentity(targetFolder);
                    await _storageService.UpdateAssignmentIdentityAsync(targetFolder, vol, fid);
                }
            }
            catch (Exception ex)
            {
                App.Log($"Non-critical library tracking failure: {ex.Message}");
            }

            await _storageService.UpdateJournalStateAsync(operationId, JournalState.Completed);

            // F21: Update LastAppliedRenderKey pointer only after successful durable apply commit
            if (!string.IsNullOrWhiteSpace(stagedResult.RenderKey))
            {
                try
                {
                    await _storageService.SetLastAppliedRenderKeyAsync(stagedResult.RenderKey);
                }
                catch (Exception ex)
                {
                    App.Log($"Non-critical LastAppliedRenderKey save failure: {ex.Message}");
                }
            }

            // Set final success icon preview from canonical path or staged path
            SuccessIconImage.Source = !string.IsNullOrEmpty(canonicalIcoPath) && File.Exists(canonicalIcoPath)
                ? LoadBitmapSafe(canonicalIcoPath)
                : LoadBitmapSafe(stagedIcoPath);

            // Clean up staged temp ICO
            CleanupStagedFiles();

            // Fire final targeted shell refresh now that all staged files are closed and committed
            ShellRefreshService.NotifyFolderChanged(targetFolder);

            App.Log($"CommitStagedIconAsync completed successfully for '{targetFolder}'");

            // The first-use hint has done its job once an apply actually happened, so it never
            // reappears on a folder this user customizes later.
            await DismissFirstUseHintAsync();

            // Persist recent custom color on successful folder apply (F02)
            if (!string.IsNullOrWhiteSpace(stagedColorHex))
            {
                try
                {
                    var userSettings = await _storageService.GetUserSettingsAsync();
                    var updatedSettings = userSettings.WithRecentCustomColor(stagedColorHex);
                    await _storageService.SaveUserSettingsAsync(updatedSettings);
                }
                catch (Exception ex)
                {
                    App.Log($"Failed to record recent custom color: {ex.Message}");
                }
            }

            // 4. Done / Success: Restrained success state with auto-close timer per spec
            SetProcessingStage(ProcessingStage.Done);
            ResetButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            App.Log($"CommitStagedIconAsync unhandled exception: {ex}");

            if (filesystemCommitted)
            {
                // The icon is already on disk. Record a repairable state instead of
                // claiming a clean failure, because a retry would re-apply on top of a
                // committed mutation.
                try
                {
                    await _storageService.UpdateJournalStateAsync(operationId, JournalState.NeedsRepair, ex.ToString());
                }
                catch (Exception journalEx)
                {
                    App.Log($"Failed to mark journal NeedsRepair: {journalEx.Message}");
                }

                ShowCategorizedError(
                    QuickPanelErrorType.FolderApplyFailed,
                    QuickPanelErrorCopy.Of(
                        AppResources.GetString("ErrorAppliedNeedsRepair", _currentLanguage),
                        ex.Message));
            }
            else
            {
                ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Diagnostic(ex.Message));
            }
        }
        finally
        {
            _isCommitting = false;
            if (string.Equals(_currentFolderPath, targetFolder, StringComparison.OrdinalIgnoreCase))
            {
                _currentBaseline = baseline;
                _currentOwnershipEpochId = epochId;
                _stagedCustomColorHex = null;
            }

            // A newer activation arrived during this commit. Apply it only now, when the
            // previous commit has fully finished, so target identity cannot interleave.
            if (_pendingTargetPath is { } pendingTarget)
            {
                _pendingTargetPath = null;
                try
                {
                    await ApplyTargetFolderAsync(pendingTarget);
                }
                catch (Exception ex)
                {
                    App.Log($"Deferred target switch failed for '{pendingTarget}': {ex}");
                }
            }
        }
    }

    private async void OnUndoClicked(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || _currentBaseline == null || string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            return;
        }

        try
        {
            SetProcessingStage(ProcessingStage.ApplyingIcon);

            var baseline = _currentBaseline;
            var targetFolder = _currentFolderPath;
            var result = await Task.Run(() => _folderCustomizer.UndoIcon(targetFolder, baseline));
            if (result.IsSuccess)
            {
                await _storageService.RemoveFolderAssignmentAsync(targetFolder);
                _currentBaseline = null;
                ResetButton.Visibility = Visibility.Collapsed;
                PreviewIconImage.Source = null;
                SuccessIconImage.Source = null;
                _stagingModel.Initialize(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
                SetProcessingStage(ProcessingStage.Idle);
            }
            else
            {
                ShowServiceError(QuickPanelErrorType.FolderApplyFailed, result.DiagnosticDescription, "ErrorUndoFailed");
            }
        }
        catch (Exception ex)
        {
            App.Log($"OnUndoClicked exception: {ex}");
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Diagnostic(ex.Message));
        }
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || _currentBaseline == null || string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            return;
        }

        try
        {
            SetProcessingStage(ProcessingStage.ApplyingIcon);

            var baseline = _currentBaseline;
            var targetFolder = _currentFolderPath;
            var result = await Task.Run(() => _folderCustomizer.ResetIcon(targetFolder, baseline));
            if (result.IsSuccess)
            {
                await _storageService.RemoveFolderAssignmentAsync(targetFolder);
                _currentBaseline = null;
                ResetButton.Visibility = Visibility.Collapsed;
                PreviewIconImage.Source = null;
                SuccessIconImage.Source = null;
                _stagingModel.Initialize(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
                SetProcessingStage(ProcessingStage.Idle);
            }
            else
            {
                ShowServiceError(QuickPanelErrorType.FolderApplyFailed, result.DiagnosticDescription, "ErrorResetFailed");
            }
        }
        catch (Exception ex)
        {
            App.Log($"OnResetClicked exception: {ex}");
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Diagnostic(ex.Message));
        }
    }


    private async void OnTryAgainClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnTryAgainClicked), () => { SetProcessingStage(ProcessingStage.Idle); return Task.CompletedTask; });
    }
}