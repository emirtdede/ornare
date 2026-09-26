using System;

namespace Ornare.Imaging;

/// <summary>
/// Immutable representation of a color in the cylindrical OKLCH color space.
/// L: Perceived lightness [0, 1]
/// C: Chroma (distance from neutral axis) [0, ~0.4]
/// H: Hue angle in degrees [0, 360)
/// </summary>
public readonly record struct OklchColor(double L, double C, double H)
{
    public override string ToString() => $"Oklch(L={L:F4}, C={C:F4}, h={H:F2}°)";
}
