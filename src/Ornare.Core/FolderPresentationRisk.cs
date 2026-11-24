namespace Ornare.Core;

/// <summary>
/// Why Windows may ignore a correct <c>desktop.ini</c>.
/// <para>
/// Since the June 2026 security updates (KB5094126 on Windows 11 24H2 and 25H2), Explorer
/// ignores a <c>desktop.ini</c> whose origin it cannot verify: a file carrying the Mark of the
/// Web, or one reached through a remote location it does not trust. The icon is then simply
/// not rendered, even though the file content is valid. Reporting "Applied" in that situation
/// is a false success, so the risk is carried in the result and checked during repair.
/// </para>
/// </summary>
public enum FolderPresentationRisk
{
    /// <summary>The customization is expected to render.</summary>
    None = 0,

    /// <summary>
    /// The file carries a <c>Zone.Identifier</c> alternate data stream, so Explorer treats it
    /// as downloaded and ignores it.
    /// </summary>
    MarkOfTheWeb,

    /// <summary>
    /// The folder is reached through a remote or mapped network location. Explorer honours
    /// <c>desktop.ini</c> there only when the location is in a trusted zone.
    /// </summary>
    RemoteLocation
}
