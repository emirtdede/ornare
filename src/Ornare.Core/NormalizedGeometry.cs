using System;

namespace Ornare.Core;

/// <summary>
/// Immutable normalized coordinate rectangle (values in range [0.0, 1.0]).
/// Represents relative zones (emblem safe zones, badge exclusion areas)
/// independent of pixel canvas dimensions.
/// </summary>
public sealed record NormalizedRect
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public NormalizedRect() { }

    public NormalizedRect(double x, double y, double width, double height)
    {
        if (x < 0.0 || x > 1.0) throw new ArgumentOutOfRangeException(nameof(x), "X must be within [0.0, 1.0].");
        if (y < 0.0 || y > 1.0) throw new ArgumentOutOfRangeException(nameof(y), "Y must be within [0.0, 1.0].");
        if (width <= 0.0 || width > 1.0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be within (0.0, 1.0].");
        if (height <= 0.0 || height > 1.0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be within (0.0, 1.0].");
        if (x + width > 1.0001) throw new ArgumentException("X + Width exceeds 1.0.");
        if (y + height > 1.0001) throw new ArgumentException("Y + Height exceeds 1.0.");

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>
    /// Checks if this normalized rectangle overlaps with another normalized rectangle.
    /// </summary>
    public bool Intersects(NormalizedRect other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !(X + Width <= other.X ||
                 other.X + other.Width <= X ||
                 Y + Height <= other.Y ||
                 other.Y + other.Height <= Y);
    }

    /// <summary>
    /// Alias for Intersects.
    /// </summary>
    public bool IntersectsWith(NormalizedRect other) => Intersects(other);

    /// <summary>
    /// Checks if a normalized point (x, y) is inside this rectangle.
    /// </summary>
    public bool Contains(double x, double y)
    {
        return x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }
}
