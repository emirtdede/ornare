namespace Ornare.Core;

/// <summary>
/// Specifies the canvas background compositing mode for custom folder icons (F08).
/// </summary>
public enum BackgroundMode
{
    /// <summary>
    /// Default mode preserving source asset transparency.
    /// </summary>
    Transparent = 0,

    /// <summary>
    /// Composites the source asset over an opaque solid background color (#RRGGBB).
    /// </summary>
    SolidColor = 1
}
