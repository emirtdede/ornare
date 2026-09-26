using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Ornare.UiTests;

public class KeyboardNavigationAndAccessibilityTests
{
    private static string? GetAppDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        string current = baseDir;
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(current, "src", "Ornare.App");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            string? parent = Path.GetDirectoryName(current);
            if (parent == null) break;
            current = parent;
        }
        return null;
    }

    [Fact]
    public void QuickPanelKeyboardNavigationAndShortcutsAreConfiguredInXaml()
    {
        string? appDir = GetAppDirectory();
        if (appDir == null) return;

        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"QuickPanelWindow.xaml not found at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);
        var ns = doc.Root!.Name.Namespace;

        // Verify DropAreaBorder is a keyboard tabstop with system focus visuals and KeyDown
        var dropArea = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "DropAreaBorder");
        Assert.NotNull(dropArea);
        Assert.Equal("True", dropArea!.Attribute("IsTabStop")?.Value);
        Assert.Equal("True", dropArea.Attribute("UseSystemFocusVisuals")?.Value);
        Assert.Equal("OnDropAreaKeyDown", dropArea.Attribute("KeyDown")?.Value);
        Assert.False(string.IsNullOrWhiteSpace(dropArea.Attribute("AutomationProperties.Name")?.Value));

        // Verify Tooltips with shortcut hints
        var applyBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "ApplyConfirmButton");
        Assert.NotNull(applyBtn);
        Assert.Contains("Enter", applyBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");

        var undoBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "UndoButton");
        Assert.NotNull(undoBtn);
        Assert.Contains("Ctrl+Z", undoBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");

        var resetBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "ResetButton");
        Assert.NotNull(resetBtn);
        Assert.Contains("Ctrl+Z", resetBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");

        var mgrBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "OpenManagerButton");
        Assert.NotNull(mgrBtn);
        Assert.Contains("Ctrl+M", mgrBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");

        var closeSuccessBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "CloseSuccessButton");
        Assert.NotNull(closeSuccessBtn);
        Assert.Contains("Esc", closeSuccessBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");
    }

    [Fact]
    public void ManagerWindowKeyboardNavigationAndShortcutsAreConfiguredInXaml()
    {
        string? appDir = GetAppDirectory();
        if (appDir == null) return;

        string xamlPath = Path.Combine(appDir, "ManagerWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"ManagerWindow.xaml not found at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        // Verify TargetTrayDropZone is a keyboard tabstop with system focus visuals and KeyDown
        var dropZone = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "TargetTrayDropZone");
        Assert.NotNull(dropZone);
        Assert.Equal("True", dropZone!.Attribute("IsTabStop")?.Value);
        Assert.Equal("True", dropZone.Attribute("UseSystemFocusVisuals")?.Value);
        Assert.Equal("OnTargetTrayDropZoneKeyDown", dropZone.Attribute("KeyDown")?.Value);
        Assert.False(string.IsNullOrWhiteSpace(dropZone.Attribute("AutomationProperties.Name")?.Value));

        // Verify Tooltips with shortcut hints
        var searchBox = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "SearchTextBox");
        Assert.NotNull(searchBox);
        Assert.Contains("Ctrl+F", searchBox!.Attribute("ToolTipService.ToolTip")?.Value ?? "");

        var importBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "ImportIconButton");
        Assert.NotNull(importBtn);
        Assert.Contains("Ctrl+O", importBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");

        var clearBtn = doc.Descendants().FirstOrDefault(e => e.Attribute("x:Name")?.Value == "ClearStagingButton");
        Assert.NotNull(clearBtn);
        Assert.Contains("Ctrl+Z", clearBtn!.Attribute("ToolTipService.ToolTip")?.Value ?? "");
    }

    [Fact]
    public void QuickPanelWindowCodeBehindImplementsGlobalKeyboardShortcutsAndFocusRestoration()
    {
        string? appDir = GetAppDirectory();
        if (appDir == null) return;

        string csPath = Path.Combine(appDir, "QuickPanelWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"QuickPanelWindow.xaml.cs not found at {csPath}");

        string code = File.ReadAllText(csPath);

        // Verify global keyboard routing
        Assert.Contains("OnWindowKeyDown", code);
        Assert.Contains("VirtualKey.Escape", code);
        Assert.Contains("VirtualKey.V", code);
        Assert.Contains("VirtualKey.Z", code);
        Assert.Contains("VirtualKey.M", code);
        Assert.Contains("VirtualKey.Enter", code);
        Assert.Contains("VirtualKey.Space", code);

        // Verify focus restoration logic
        Assert.Contains("Focus(FocusState.Programmatic)", code);
        Assert.Contains("OnPlacementOptionsFlyoutClosed", code);
        Assert.Contains("OnCustomColorFlyoutClosed", code);
    }

    [Fact]
    public void ManagerWindowCodeBehindImplementsGlobalKeyboardShortcutsAndFocusRestoration()
    {
        string? appDir = GetAppDirectory();
        if (appDir == null) return;

        string csPath = Path.Combine(appDir, "ManagerWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"ManagerWindow.xaml.cs not found at {csPath}");

        string code = File.ReadAllText(csPath);

        // Verify global keyboard routing
        Assert.Contains("OnManagerKeyDown", code);
        Assert.Contains("VirtualKey.Escape", code);
        Assert.Contains("VirtualKey.V", code);
        Assert.Contains("VirtualKey.Z", code);
        Assert.Contains("VirtualKey.F", code);
        Assert.Contains("VirtualKey.O", code);
        Assert.Contains("OnTargetTrayDropZoneKeyDown", code);

        // Verify focus restoration logic
        Assert.Contains("Focus(FocusState.Programmatic)", code);
    }
}
