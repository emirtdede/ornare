using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;

namespace Ornare.App;

/// <summary>
/// Owns the single <see cref="ManagerWindow"/> instance for the process.
/// <para>
/// Before this host existed every "Manage" / "Settings" / "About" gesture built a
/// new <see cref="ManagerWindow"/> that was never tracked, so a user could end up
/// with several managers mutating the same SQLite store at once, with no disposal
/// and no deduplication. Routing all entry points through here keeps exactly one
/// manager alive and re-focuses it instead of stacking windows.
/// </para>
/// </summary>
internal static class ManagerWindowHost
{
    private static ManagerWindow? _window;

    /// <summary>True while a manager window is alive.</summary>
    public static bool IsOpen => _window is not null;

    /// <summary>The live manager window, or null when none is open.</summary>
    public static ManagerWindow? Current => _window;

    /// <summary>
    /// Opens (or re-focuses) the manager window on the requested section.
    /// </summary>
    public static void Show(
        IStorageService storageService,
        IFolderCustomizer folderCustomizer,
        IImagePipeline? imagePipeline = null,
        string? section = null)
    {
        if (_window is null)
        {
            _window = new ManagerWindow(storageService, folderCustomizer, imagePipeline);
            _window.Closed += (_, _) =>
            {
                _window = null;
                App.NotifyWindowClosed();
            };
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            _window.SwitchSection(section);
        }

        _window.Activate();
    }

    /// <summary>
    /// Opens the manager window directly on the Settings section.
    /// </summary>
    public static void OpenSettings(
        IStorageService storageService,
        IFolderCustomizer folderCustomizer,
        IImagePipeline? imagePipeline = null)
        => Show(storageService, folderCustomizer, imagePipeline, "Settings");
}
