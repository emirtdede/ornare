namespace Ornare.Core;

/// <summary>
/// Dictates how a source image is placed and fitted onto the icon canvas (F06).
/// </summary>
public enum ImagePlacementMode
{
    /// <summary>
    /// Default. Preserves aspect ratio, fits the complete source image within the canvas with transparent letterboxing.
    /// </summary>
    Contain = 0,

    /// <summary>
    /// Preserves aspect ratio, scales to completely cover the canvas, center-cropping the overflowing axis.
    /// </summary>
    Cover = 1,

    /// <summary>
    /// Stretches the source image to completely fill the canvas with independent X and Y scaling.
    /// Explicitly distortion-capable; requires user selection.
    /// </summary>
    Fill = 2
}
