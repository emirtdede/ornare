namespace Ornare.App;

using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Ornare.Core;

/// <summary>
/// Temporary preview and thumbnail files owned by the quick panel.
/// <para>
/// The panel renders many short-lived PNGs; this file is the one place that creates,
/// bounds and deletes them, so a leak is visible in one diff instead of hidden inside a
/// 3,200-line window class. Every file created here is reachable from <see cref="Dispose"/>.
/// </para>
/// </summary>
public sealed partial class QuickPanelWindow
{
    private void CleanupStagedFiles()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_stagedTempIcoPath) && File.Exists(_stagedTempIcoPath))
            {
                File.Delete(_stagedTempIcoPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked staging file is retried on the next cleanup pass; clearing the
            // in-memory state below is what matters for correctness.
            App.Log($"Staged ICO cleanup deferred: {ex.Message}");
        }
        _stagedTempIcoPath = null;
        _stagedProcessResult = null;
        _stagedImageBytes = null;
        _stagedPlacementMode = ImagePlacementMode.Contain;
        if (PlacementModeRadioButtons != null)
        {
            PlacementModeRadioButtons.SelectedIndex = 0;
        }
        if (PlacementFillWarningText != null)
        {
            PlacementFillWarningText.Visibility = Visibility.Collapsed;
        }
        _stagedPaddingPercent = 0;
        if (PaddingToggleSwitch != null)
        {
            PaddingToggleSwitch.IsOn = false;
        }
        if (PaddingSlider != null)
        {
            PaddingSlider.Value = 8;
        }
        if (PaddingSliderContainer != null)
        {
            PaddingSliderContainer.Visibility = Visibility.Collapsed;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_tempPreviewPngPath) && File.Exists(_tempPreviewPngPath))
            {
                File.Delete(_tempPreviewPngPath);
            }
        }
        catch (Exception ex)
        {
            // A locked preview file (still being decoded) is expected; log so an
            // accumulating temp directory is diagnosable.
            App.Log($"Preview temp delete failed: {ex.Message}");
        }
        _tempPreviewPngPath = null;
    }

    /// <summary>
    /// Returns a unique PNG path inside the per-user scratch directory, creating it first.
    /// Falling back to <c>%TEMP%</c> keeps previews working if the app-data folder is
    /// unavailable (for example a locked profile), without regressing to the temp root.
    /// </summary>
    private static string CreatePanelTempPngPath(string prefix)
    {
        try
        {
            Directory.CreateDirectory(OrnareConstants.TempDirectory);
            return Path.Combine(OrnareConstants.TempDirectory, $"{prefix}_{Guid.NewGuid():N}.png");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Log($"Panel temp directory unavailable, falling back to %TEMP%: {ex.Message}");
            return Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}.png");
        }
    }

    private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? LoadPreviewBitmapFromBytes(byte[] pngBytes)
    {
        try
        {
            string newTemp = CreatePanelTempPngPath("ornare_prev");
            File.WriteAllBytes(newTemp, pngBytes);
            string? oldTemp = _tempPreviewPngPath;
            _tempPreviewPngPath = newTemp;
            if (oldTemp != null && File.Exists(oldTemp))
            {
                try
                {
                    File.Delete(oldTemp);
                }
                catch (IOException ex)
                {
                    App.Log($"Stale preview delete failed: {ex.Message}");
                }
            }
            return LoadBitmapSafe(newTemp);
        }
        catch (Exception ex)
        {
            App.Log($"LoadPreviewBitmapFromBytes failed: {ex.Message}");
            return null;
        }
    }

    private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? LoadBitmapFromBytes(byte[] pngBytes)
    {
        try
        {
            string temp = CreatePanelTempPngPath("ornare_thumb");
            File.WriteAllBytes(temp, pngBytes);

            // Track the file so Dispose can remove it. Untracked thumbnails used to
            // accumulate in %TEMP% for every hover/render (9+ files per interaction).
            _tempThumbnailPngPaths.Add(temp);

            // Bound growth in a long-lived panel: only the most recent thumbnails
            // can still be referenced by the live template cards.
            while (_tempThumbnailPngPaths.Count > 64)
            {
                string oldest = _tempThumbnailPngPaths[0];
                _tempThumbnailPngPaths.RemoveAt(0);
                TryDeleteTempFile(oldest);
            }

            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(temp));
        }
        catch (Exception ex)
        {
            // A blank thumbnail must not be silent: surface it so temp growth or a
            // broken pipeline is visible.
            App.Log($"LoadBitmapFromBytes failed: {ex.Message}");
            return null;
        }
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            // BitmapImage may still hold the file; it will be retried on dispose.
            App.Log($"Temp thumbnail delete deferred: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            App.Log($"Temp thumbnail delete denied: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Set first: in-flight continuations check this before touching the visual tree, and
        // a disposed WinUI content tree throws COMException on any property assignment.
        _disposed = true;

        AppResources.CurrentLocaleChanged -= OnAppLanguageChanged;

        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        _previewDebounceCts = null;

        _previewRenderCts?.Cancel();
        _previewRenderCts?.Dispose();
        _previewRenderCts = null;

        _autoCloseTimer?.Stop();
        _autoCloseTimer = null;

        CleanupStagedFiles();

        foreach (string temp in _tempThumbnailPngPaths)
        {
            TryDeleteTempFile(temp);
        }

        _tempThumbnailPngPaths.Clear();

        GC.SuppressFinalize(this);
    }

    private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? LoadBitmapSafe(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UriFormatException)
        {
            // Missing/corrupt preview files degrade to "no thumbnail", never a crash.
            App.Log($"LoadBitmapSafe failed for '{path}': {ex.Message}");
        }
        return null;
    }
}