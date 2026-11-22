using Ornare.Core;
using Ornare.Windows;

namespace Ornare.App;

/// <summary>
/// Owns the notification-area icon for the running process. The tray icon exists only while an
/// Ornare window exists, so closing the manager still ends the process; it gives the chosen app
/// icon a place in the tray and lets the manager be reopened with one click.
/// </summary>
internal sealed class AppTrayIcon : IDisposable
{
    private readonly TrayIcon _icon;
    private bool _disposed;

    private AppTrayIcon(TrayIcon icon)
    {
        _icon = icon;
    }

    /// <summary>The live tray icon, or null when the shell refused to host one.</summary>
    public static AppTrayIcon? Current { get; private set; }

    /// <summary>
    /// Creates the tray icon for <paramref name="settings"/> and returns null when the icon
    /// file cannot be produced or the shell rejects the notification icon. A missing tray icon
    /// never blocks the application.
    /// </summary>
    public static AppTrayIcon? TryCreate(UserSettings settings, Action openRequested, Action exitRequested)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(openRequested);
        ArgumentNullException.ThrowIfNull(exitRequested);

        if (Current is not null)
        {
            return Current;
        }

        try
        {
            string iconPath = AppearanceService.EnsureIconFile(
                AppIconCatalog.Resolve(settings.AppIconId),
                AppearanceService.ResolveAccentHex(settings.AccentColor));

            if (string.IsNullOrEmpty(iconPath))
            {
                return null;
            }

            var tray = new TrayIcon(
                iconPath,
                "Ornare",
                BuildLabels,
                openRequested,
                exitRequested);

            if (!tray.IsVisible)
            {
                tray.Dispose();
                return null;
            }

            Current = new AppTrayIcon(tray);
            return Current;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException)
        {
            App.Log($"Tray icon unavailable: {exception.Message}");
            return null;
        }
    }

    /// <summary>Follows an app icon change without recreating the notification icon.</summary>
    public void UpdateIcon(UserSettings settings)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            string iconPath = AppearanceService.EnsureIconFile(
                AppIconCatalog.Resolve(settings.AppIconId),
                AppearanceService.ResolveAccentHex(settings.AccentColor));
            if (!string.IsNullOrEmpty(iconPath))
            {
                _icon.UpdateIcon(iconPath);
            }

            _icon.UpdateTooltip("Ornare");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            App.Log($"Tray icon update failed: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Dispose();
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    private static TrayMenuLabels BuildLabels()
    {
        string locale = AppResources.CurrentLocale;
        return new TrayMenuLabels(
            AppResources.GetString("OpenManager", locale),
            AppResources.GetString("Tray_Exit", locale));
    }
}
