namespace Ornare.Imaging;

/// <summary>
/// Provides a shared synchronization gate for SVG rasterization operations.
/// Prevents concurrent execution of native SVG delegates across threads and test runners.
/// </summary>
internal static class SvgRasterizationGate
{
    public static readonly object Lock = new();
}
