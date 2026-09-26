using System;

namespace Ornare.Imaging;

/// <summary>
/// Immutable representation of a color in the OKLab color space (Björn Ottosson, 2020).
/// L: Perceived lightness [0, 1]
/// a: Green-red opponent axis
/// b: Blue-yellow opponent axis
/// </summary>
public readonly record struct OklabColor(double L, double A, double B)
{
    public override string ToString() => $"Oklab(L={L:F4}, a={A:F4}, b={B:F4})";
}
