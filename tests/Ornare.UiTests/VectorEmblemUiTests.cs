using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Ornare.UiTests;

public class VectorEmblemUiTests
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
    public void VectorEmblemItemViewModelSourceCodeImplementsExpectedPropertiesAndEvents()
    {
        string appDir = FindAppDirectory();
        string vmPath = Path.Combine(appDir, "VectorEmblemItemViewModel.cs");
        Assert.True(File.Exists(vmPath), $"VectorEmblemItemViewModel.cs not found at {vmPath}");

        string code = File.ReadAllText(vmPath);

        Assert.Contains("INotifyPropertyChanged", code);
        Assert.Contains("public string Id { get; }", code);
        Assert.Contains("public string ShortId { get; }", code);
        Assert.Contains("public string DisplayName { get; }", code);
        Assert.Contains("public string Category { get; }", code);
        Assert.Contains("public string PathData { get; }", code);
        Assert.Contains("public Geometry? GeometryData", code);
        Assert.Contains("public bool IsSelected", code);
        Assert.Contains("public Brush? BorderBrush", code);
        Assert.Contains("public Brush? Background", code);
    }

    [Fact]
    public void QuickPanelWindowXamlConfiguresIconFlyoutWithSearchAndFilters()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"QuickPanelWindow.xaml not found at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        // Find IconFlyout
        var flyout = doc.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "Flyout" &&
            (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "IconFlyout");
        Assert.NotNull(flyout);

        // Verify key controls exist inside IconFlyout
        var xNames = doc.Descendants()
            .Select(e => (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")))
            .Where(n => n != null)
            .ToHashSet();

        Assert.Contains("RemoveEmblemButton", xNames);
        Assert.Contains("IconSearchTextBox", xNames);
        Assert.Contains("IconCategoryComboBox", xNames);
        Assert.Contains("EmblemColorAutoButton", xNames);
        Assert.Contains("EmblemColorWhiteButton", xNames);
        Assert.Contains("EmblemColorBlackButton", xNames);
        Assert.Contains("IconGridView", xNames);
    }

    [Fact]
    public void QuickPanelWindowCodeBehindImplementsVectorEmblemLifecycle()
    {
        string appDir = FindAppDirectory();
        // The window is a partial class across several files (placement, temp assets, catalogs,
        // localization). Reading only the main file would fail for a method that legitimately
        // lives in a partial, so the guard reads the whole class.
        var files = Directory.EnumerateFiles(appDir, "QuickPanelWindow*.cs").OrderBy(p => p).ToList();
        Assert.True(files.Count > 0, $"QuickPanelWindow partials not found in {appDir}");

        string code = string.Join("\n", files.Select(File.ReadAllText));

        Assert.Contains("ObservableCollection<VectorEmblemItemViewModel> VectorEmblemCards", code);
        Assert.Contains("OnIconFlyoutOpening", code);
        Assert.Contains("EnsureEmblemCatalogInitialized", code);
        Assert.Contains("SyncEmblemSelectionState", code);
        Assert.Contains("OnIconSearchTextChanged", code);
        Assert.Contains("OnIconCategorySelectionChanged", code);
        Assert.Contains("OnIconGridItemClick", code);
        Assert.Contains("OnRemoveEmblemClicked", code);
        Assert.Contains("OnEmblemColorAutoClicked", code);
        Assert.Contains("OnEmblemColorWhiteClicked", code);
        Assert.Contains("OnEmblemColorBlackClicked", code);
    }
}
