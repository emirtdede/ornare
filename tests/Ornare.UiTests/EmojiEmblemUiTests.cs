using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Ornare.UiTests;

public class EmojiEmblemUiTests
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
    public void EmojiItemViewModelSourceCodeImplementsExpectedPropertiesAndEvents()
    {
        string appDir = FindAppDirectory();
        string vmPath = Path.Combine(appDir, "EmojiItemViewModel.cs");
        Assert.True(File.Exists(vmPath), $"EmojiItemViewModel.cs not found at {vmPath}");

        string code = File.ReadAllText(vmPath);

        Assert.Contains("INotifyPropertyChanged", code);
        Assert.Contains("public string Grapheme { get; }", code);
        Assert.Contains("public string Name { get; }", code);
        Assert.Contains("public string Category { get; }", code);
        Assert.Contains("public bool IsSelected", code);
        Assert.Contains("public Brush? BorderBrush", code);
        Assert.Contains("public Brush? Background", code);
    }

    [Fact]
    public void QuickPanelWindowXamlConfiguresEmojiFlyoutWithSearchAndFilters()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"QuickPanelWindow.xaml not found at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        // Find EmojiFlyout
        var flyout = doc.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "Flyout" &&
            (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EmojiFlyout");

        Assert.NotNull(flyout);

        // Find Search box
        var searchBox = flyout.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "TextBox" &&
            (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EmojiSearchTextBox");
        Assert.NotNull(searchBox);

        // Find Category ComboBox
        var categoryCombo = flyout.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "ComboBox" &&
            (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EmojiCategoryComboBox");
        Assert.NotNull(categoryCombo);

        // Find Emoji GridView
        var gridView = flyout.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "GridView" &&
            (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EmojiGridView");
        Assert.NotNull(gridView);

        // Verify Segoe UI Emoji font is specified on TextBlock
        var textBlocks = gridView.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();
        Assert.Contains(textBlocks, tb => (string?)tb.Attribute("FontFamily") == "Segoe UI Emoji");
    }

    [Fact]
    public void QuickPanelCodeBehindImplementsEmojiCatalogLazyLoadingAndFilter()
    {
        string appDir = FindAppDirectory();
        string csPath = Path.Combine(appDir, "QuickPanelWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"QuickPanelWindow.xaml.cs not found at {csPath}");

        string code = File.ReadAllText(csPath);

        Assert.Contains("ObservableCollection<EmojiItemViewModel> EmojiCards", code);
        Assert.Contains("OnEmojiFlyoutOpening", code);
        Assert.Contains("EnsureEmojiCatalogInitialized", code);
        Assert.Contains("SyncEmojiSelectionState", code);
        Assert.Contains("OnEmojiSearchTextChanged", code);
        Assert.Contains("OnEmojiCategorySelectionChanged", code);
        Assert.Contains("FilterEmojiCards", code);
        Assert.Contains("OnEmojiGridItemClick", code);
        Assert.Contains("_stagingModel.StageEmojiEmblem", code);
    }
}
