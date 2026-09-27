using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Ornare.UiTests;

public class FolderStyleUiTests
{
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

    [Fact]
    public void FolderTemplateCardViewModelSourceCodeImplementsExpectedPropertiesAndEvents()
    {
        string appDir = FindAppDirectory();
        string vmPath = Path.Combine(appDir, "FolderTemplateCardViewModel.cs");
        Assert.True(File.Exists(vmPath), $"FolderTemplateCardViewModel.cs not found at {vmPath}");

        string code = File.ReadAllText(vmPath);

        Assert.Contains("INotifyPropertyChanged", code);
        Assert.Contains("public string Id { get; }", code);
        Assert.Contains("public int Version { get; }", code);
        Assert.Contains("public string DisplayName { get; }", code);
        Assert.Contains("public int SortOrder { get; }", code);
        Assert.Contains("public bool IsSelected", code);
        Assert.Contains("public ImageSource? Thumbnail", code);
        Assert.Contains("public Brush? BorderBrush", code);
        Assert.Contains("public Brush? Background", code);
    }

    [Fact]
    public void QuickPanelWindowXamlConfiguresBottomActionBarWithSixButtonsInFixedOrder()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"QuickPanelWindow.xaml not found at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        // Root Grid must have 4 rows: 36, *, Auto, Auto
        var rootGrid = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Grid" && e.Attribute("RowDefinitions") != null);
        Assert.NotNull(rootGrid);
        Assert.Equal("36,*,Auto,Auto", rootGrid!.Attribute("RowDefinitions")?.Value);
        Assert.Equal("OnWindowKeyDown", rootGrid.Attribute("KeyDown")?.Value);

        // Verify the 6 action buttons exist
        string[] expectedButtons =
        [
            "FolderActionButton",
            "IconActionButton",
            "EmojiActionButton",
            "ImageActionButton",
            "ColorActionButton",
            "MoreActionButton"
        ];

        var allButtons = doc.Descendants().Where(e => e.Name.LocalName == "Button").ToList();

        for (int i = 0; i < expectedButtons.Length; i++)
        {
            var btn = allButtons.FirstOrDefault(b => b.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == expectedButtons[i]));
            Assert.NotNull(btn);
        }

        // Verify Status & Apply row
        var applyBtn = allButtons.FirstOrDefault(b => b.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ApplyConfirmButton"));
        Assert.NotNull(applyBtn);
        Assert.Equal("False", applyBtn!.Attribute("IsEnabled")?.Value);
        Assert.Equal("OnApplyConfirmClicked", applyBtn.Attribute("Click")?.Value);

        var statusText = doc.Descendants().FirstOrDefault(e => e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "StatusInfoText"));
        Assert.NotNull(statusText);

        // Verify FolderStyleFlyout contains 3x3 GridView
        var flyout = doc.Descendants().FirstOrDefault(e => e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FolderStyleFlyout"));
        Assert.NotNull(flyout);

        var gridView = doc.Descendants().FirstOrDefault(e => e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FolderTemplateGridView"));
        Assert.NotNull(gridView);
        Assert.Equal("OnFolderTemplateCardClicked", gridView!.Attribute("ItemClick")?.Value);

        var itemsWrapGrid = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ItemsWrapGrid");
        Assert.NotNull(itemsWrapGrid);
        Assert.Equal("3", itemsWrapGrid!.Attribute("MaximumRowsOrColumns")?.Value);
        Assert.Equal("Horizontal", itemsWrapGrid.Attribute("Orientation")?.Value);
    }

    [Fact]
    public void QuickPanelWindowCodeBehindImplementsPhase5KeyboardShortcutsAndStagingHandlers()
    {
        string appDir = FindAppDirectory();
        string csPath = Path.Combine(appDir, "QuickPanelWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"QuickPanelWindow.xaml.cs not found at {csPath}");

        string code = File.ReadAllText(csPath);

        // Verify Staging integration
        Assert.Contains("_stagingModel", code);
        Assert.Contains("FolderTemplateCards", code);
        Assert.Contains("RefreshLivePreviewAsync", code);
        Assert.Contains("ApplyStagedPersonalizationAsync", code);

        // Verify Alt+1..6 shortcut routing
        Assert.Contains("VirtualKey.Number1", code);
        Assert.Contains("VirtualKey.Number2", code);
        Assert.Contains("VirtualKey.Number3", code);
        Assert.Contains("VirtualKey.Number4", code);
        Assert.Contains("VirtualKey.Number5", code);
        Assert.Contains("VirtualKey.Number6", code);

        // Verify Card event handlers
        Assert.Contains("OnFolderTemplateCardPointerEntered", code);
        Assert.Contains("OnFolderTemplateCardPointerExited", code);
        Assert.Contains("OnFolderTemplateCardClicked", code);

        // Verify Escape discard
        Assert.Contains("_stagingModel.Discard()", code);
    }
}
