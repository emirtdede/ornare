using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ornare.DesignSystem;
using Xunit;

namespace Ornare.UiTests;

public sealed class Phase10AccessibilityAndUiQualificationTests
{
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string FindAppDirectory()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "src", "Ornare.App");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        throw new DirectoryNotFoundException("Could not find src/Ornare.App from BaseDirectory: " + AppContext.BaseDirectory);
    }

    private static string? GetAttributeValue(XElement element, string localName)
    {
        return element.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;
    }

    [Fact]
    public void BottomActionBarKeyboardShortcutsAlt1Through6AreWiredInCodeBehind()
    {
        string appDir = FindAppDirectory();
        string csPath = Path.Combine(appDir, "QuickPanelWindow.xaml.cs");
        Assert.True(File.Exists(csPath));

        string code = File.ReadAllText(csPath);

        // Verify Alt modifier check
        Assert.Contains("VirtualKey.Menu", code);

        // Verify Alt+1..6 cases in OnWindowKeyDown
        Assert.Contains("case global::Windows.System.VirtualKey.Number1:", code);
        Assert.Contains("FolderStyleFlyout?.ShowAt(FolderActionButton);", code);

        Assert.Contains("case global::Windows.System.VirtualKey.Number2:", code);
        Assert.Contains("IconFlyout?.ShowAt(IconActionButton);", code);

        Assert.Contains("case global::Windows.System.VirtualKey.Number3:", code);
        Assert.Contains("EmojiFlyout?.ShowAt(EmojiActionButton);", code);

        Assert.Contains("case global::Windows.System.VirtualKey.Number4:", code);
        Assert.Contains("OpenFilePickerAsync()", code);

        Assert.Contains("case global::Windows.System.VirtualKey.Number5:", code);
        Assert.Contains("CustomColorFlyout?.ShowAt(ColorActionButton);", code);

        Assert.Contains("case global::Windows.System.VirtualKey.Number6:", code);
        Assert.Contains("MoreMenuFlyout?.ShowAt(MoreActionButton);", code);

        // Verify Enter / Ctrl+Enter and Escape
        Assert.Contains("VirtualKey.Enter", code);
        Assert.Contains("ApplyStagedPersonalizationAsync", code);
        Assert.Contains("VirtualKey.Escape", code);
        Assert.Contains("HandleEscapeKey()", code);
    }

    [Fact]
    public void AllInteractiveControlsInQuickPanelHaveAccessibleAutomationNames()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        Assert.True(File.Exists(xamlPath));

        var doc = XDocument.Parse(File.ReadAllText(xamlPath));

        // Required interactive buttons on the main panel
        string[] requiredNamedButtons =
        [
            "ResetButton",
            "OpenManagerButton",
            "FolderActionButton",
            "IconActionButton",
            "EmojiActionButton",
            "ImageActionButton",
            "ColorActionButton",
            "MoreActionButton",
            "ApplyConfirmButton",
            "CancelPreviewButton"
        ];

        foreach (var btnName in requiredNamedButtons)
        {
            var btn = doc.Descendants().FirstOrDefault(e => GetAttributeValue(e, "Name") == btnName);
            Assert.NotNull(btn);

            string? uiaName = GetAttributeValue(btn!, "Name") != null
                ? btn!.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name" && a.Name.NamespaceName.Contains("AutomationProperties"))?.Value
                  ?? btn!.Attributes().FirstOrDefault(a => a.Name.ToString().Contains("AutomationProperties.Name"))?.Value
                  ?? GetAttributeValue(btn!, "AutomationProperties.Name")
                : null;

            // Direct search for AutomationProperties.Name attribute
            if (string.IsNullOrEmpty(uiaName))
            {
                var attr = btn!.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.Name" || a.Name.ToString().EndsWith("AutomationProperties.Name", StringComparison.Ordinal));
                uiaName = attr?.Value;
            }

            Assert.False(string.IsNullOrWhiteSpace(uiaName), $"Button '{btnName}' missing AutomationProperties.Name");
        }
    }

    [Fact]
    public void BottomActionBarTooltipsIncludeKeyboardShortcutHints()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(xamlPath));

        (string ButtonName, string ExpectedShortcut)[] buttonShortcuts =
        [
            ("FolderActionButton", "Alt+1"),
            ("IconActionButton", "Alt+2"),
            ("EmojiActionButton", "Alt+3"),
            ("ImageActionButton", "Alt+4"),
            ("ColorActionButton", "Alt+5"),
            ("MoreActionButton", "Alt+6"),
            ("ApplyConfirmButton", "Enter"),
            ("CancelPreviewButton", "Esc")
        ];

        foreach (var (btnName, shortcut) in buttonShortcuts)
        {
            var btn = doc.Descendants().FirstOrDefault(e => GetAttributeValue(e, "Name") == btnName);
            Assert.NotNull(btn);

            var tooltipAttr = btn!.Attributes().FirstOrDefault(a => a.Name.LocalName == "ToolTip" || a.Name.ToString().Contains("ToolTipService.ToolTip"));
            Assert.NotNull(tooltipAttr);
            Assert.Contains(shortcut, tooltipAttr.Value);
        }
    }

    [Fact]
    public void HighContrastAndThemeAdaptabilityViaThemeResources()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        string xaml = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xaml);

        // Root Grid inside Window must use ThemeResource for Background
        var mainGrid = doc.Root!.Elements().FirstOrDefault(e => e.Name.LocalName == "Grid");
        Assert.NotNull(mainGrid);
        string? bg = GetAttributeValue(mainGrid!, "Background");
        Assert.NotNull(bg);
        Assert.Contains("ThemeResource", bg);

        // Verify key text and foreground brushes use ThemeResource
        var textBlocks = doc.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();
        Assert.NotEmpty(textBlocks);
        foreach (var tb in textBlocks)
        {
            string? fg = GetAttributeValue(tb, "Foreground");
            if (fg != null && fg.StartsWith('{'))
            {
                Assert.Contains("ThemeResource", fg);
            }
        }
    }

    [Theory]
    [InlineData(1.00)] // 100% DPI (96 DPI)
    [InlineData(1.25)] // 125% DPI (120 DPI)
    [InlineData(1.50)] // 150% DPI (144 DPI)
    [InlineData(1.75)] // 175% DPI (168 DPI)
    [InlineData(2.00)] // 200% DPI (192 DPI)
    public void DpiScalingLayoutCalculationsPreserveVisualProportions(double scaleFactor)
    {
        int logicalWidth = (int)DesignTokens.QuickPanelWidth;   // 420
        int logicalHeight = (int)DesignTokens.QuickPanelHeight; // 330

        int physicalWidth = (int)Math.Round(logicalWidth * scaleFactor);
        int physicalHeight = (int)Math.Round(logicalHeight * scaleFactor);

        Assert.True(physicalWidth >= 420);
        Assert.True(physicalHeight >= 330);

        // Aspect ratio must be exactly maintained within rounding precision
        double logicalAspect = (double)logicalWidth / logicalHeight;
        double physicalAspect = (double)physicalWidth / physicalHeight;
        Assert.True(Math.Abs(logicalAspect - physicalAspect) < 0.01);

        // Test clamping on simulated display at this DPI scale
        var scaledWorkArea = new QuickPanelPositioningTests.TestRect
        {
            Left = 0,
            Top = 0,
            Right = (int)(1920 * scaleFactor),
            Bottom = (int)(1040 * scaleFactor)
        };

        var (clampedX, clampedY) = QuickPanelPositioningTests.CalculateWindowPosition(
            scaledWorkArea.Right - 5,
            scaledWorkArea.Bottom - 5,
            scaledWorkArea,
            physicalWidth,
            physicalHeight,
            margin: (int)(12 * scaleFactor));

        Assert.True(clampedX + physicalWidth <= scaledWorkArea.Right);
        Assert.True(clampedY + physicalHeight <= scaledWorkArea.Bottom);
    }

    [Fact]
    public void TurkishAndEnglishTypographyParityAndAbsenceOfMojibake()
    {
        foreach (string locale in new[] { "en-US", "tr-TR" })
        {
            Dictionary<string, string> values = RuntimeResourceSource.LoadLocale(locale);
            Assert.NotEmpty(values);

            foreach ((string key, string value) in values)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"Empty value in {locale} for '{key}'.");

                // U+FFFD is what a failed UTF-8 decode leaves behind. A stray "Ã" or "Â" is
                // the classic Latin-1 double-encoding of a UTF-8 lead byte. Either one in a
                // shipped string means the copy was decoded with the wrong code page.
                Assert.DoesNotContain("\uFFFD", value);
                Assert.DoesNotContain("Ã", value);
                Assert.DoesNotContain("Â", value);
            }
        }
    }
}
