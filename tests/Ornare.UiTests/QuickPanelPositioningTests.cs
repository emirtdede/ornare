using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Ornare.DesignSystem;
using Xunit;

namespace Ornare.UiTests;

public class QuickPanelPositioningTests
{
    public struct TestRect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public static (int X, int Y) CalculateWindowPosition(int? cursorX, int? cursorY, TestRect workArea, int windowWidth, int windowHeight, int margin = 12)
    {
        int targetX;
        int targetY;

        if (cursorX.HasValue && cursorY.HasValue)
        {
            // Center window around cursor
            targetX = cursorX.Value - (windowWidth / 2);
            targetY = cursorY.Value - (windowHeight / 2);

            // Clamp to work area
            if (targetX + windowWidth > workArea.Right - margin)
            {
                targetX = workArea.Right - windowWidth - margin;
            }
            if (targetX < workArea.Left + margin)
            {
                targetX = workArea.Left + margin;
            }

            if (targetY + windowHeight > workArea.Bottom - margin)
            {
                targetY = workArea.Bottom - windowHeight - margin;
            }
            if (targetY < workArea.Top + margin)
            {
                targetY = workArea.Top + margin;
            }
        }
        else
        {
            // Fallback: center in work area
            targetX = workArea.Left + (workArea.Width - windowWidth) / 2;
            targetY = workArea.Top + (workArea.Height - windowHeight) / 2;
        }

        return (targetX, targetY);
    }

    [Fact]
    public void CenteredCursorPositionsWindowDirectlyUnderCursor()
    {
        var work = new TestRect { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        int width = (int)DesignTokens.QuickPanelWidth; // 420
        int height = (int)DesignTokens.QuickPanelHeight; // 330

        var (x, y) = CalculateWindowPosition(960, 520, work, width, height);

        Assert.Equal(960 - (width / 2), x);
        Assert.Equal(520 - (height / 2), y);
    }

    [Fact]
    public void RightAndBottomScreenEdgesAreClampedSafely()
    {
        var work = new TestRect { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        int width = (int)DesignTokens.QuickPanelWidth; // 420
        int height = (int)DesignTokens.QuickPanelHeight; // 330

        // Cursor at bottom-right corner
        var (x, y) = CalculateWindowPosition(1915, 1035, work, width, height);

        Assert.Equal(1920 - width - 12, x);
        Assert.Equal(1040 - height - 12, y);
    }

    [Fact]
    public void LeftAndTopScreenEdgesAreClampedSafely()
    {
        var work = new TestRect { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        int width = (int)DesignTokens.QuickPanelWidth; // 420
        int height = (int)DesignTokens.QuickPanelHeight; // 330

        // Cursor at top-left corner
        var (x, y) = CalculateWindowPosition(5, 5, work, width, height);

        Assert.Equal(12, x);
        Assert.Equal(12, y);
    }

    [Fact]
    public void MultiMonitorNegativeCoordinatesAreHandledCorrectly()
    {
        // Secondary monitor to the left: -1920 to 0
        var secondaryWork = new TestRect { Left = -1920, Top = 0, Right = 0, Bottom = 1080 };
        int width = 420;
        int height = 330;

        // Cursor in middle of secondary monitor
        var (x, y) = CalculateWindowPosition(-1000, 500, secondaryWork, width, height);
        Assert.Equal(-1000 - 210, x);
        Assert.Equal(500 - 165, y);

        // Cursor near left edge of secondary monitor
        var (clampedX, _) = CalculateWindowPosition(-1915, 500, secondaryWork, width, height);
        Assert.Equal(-1920 + 12, clampedX);
    }

    [Fact]
    public void FallbackCentersOnWorkAreaWhenCoordinatesMissing()
    {
        var work = new TestRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        int width = 420;
        int height = 330;

        var (x, y) = CalculateWindowPosition(null, null, work, width, height);

        Assert.Equal((1920 - 420) / 2, x);
        Assert.Equal((1080 - 330) / 2, y);
    }

    [Fact]
    public void ProtocolUriParametersExtractPathAndCursorCoordinates()
    {
        string uriString = "ornare://set-folder?v=1&path=C%3A%5CUsers%5Ctest%5CDesktop%5CProjects&x=1420&y=780";
        var parsedUri = new Uri(uriString);

        var pathMatch = Regex.Match(parsedUri.Query, @"[?&]path=([^&]+)");
        Assert.True(pathMatch.Success);
        string extractedPath = Uri.UnescapeDataString(pathMatch.Groups[1].Value);
        Assert.Equal(@"C:\Users\test\Desktop\Projects", extractedPath);

        var xMatch = Regex.Match(parsedUri.Query, @"[?&]x=(-?\d+)");
        Assert.True(xMatch.Success);
        int x = int.Parse(xMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.Equal(1420, x);

        var yMatch = Regex.Match(parsedUri.Query, @"[?&]y=(-?\d+)");
        Assert.True(yMatch.Success);
        int y = int.Parse(yMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.Equal(780, y);
    }
}
