using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ornare.Core;
using Xunit;

namespace Ornare.UiTests;

public class Phase8ImageColorMoreIntegrationTests
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
    public void QuickPanelWindowXamlConfiguresMoreMenuWithFiveRequiredItemsInOrder()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"QuickPanelWindow.xaml not found at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        var moreFlyout = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MenuFlyout" && (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "MoreMenuFlyout");
        Assert.NotNull(moreFlyout);

        var items = moreFlyout!.Elements().ToList();
        // Required 5 items + 1 separator:
        // 1. Remove Emblem (MenuFlyoutItem)
        // 2. Reset Folder Customization (MenuFlyoutItem)
        // 3. Separator (MenuFlyoutSeparator)
        // 4. Open Manager (MenuFlyoutItem)
        // 5. Settings (MenuFlyoutItem)
        // 6. About / Open-source acknowledgements (MenuFlyoutItem)
        Assert.Equal(6, items.Count);

        Assert.Equal("MenuFlyoutItem", items[0].Name.LocalName);
        Assert.Equal("MoreRemoveEmblemMenuItem", (string?)items[0].Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));

        Assert.Equal("MenuFlyoutItem", items[1].Name.LocalName);
        Assert.Equal("MoreResetMenuItem", (string?)items[1].Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));

        Assert.Equal("MenuFlyoutSeparator", items[2].Name.LocalName);

        Assert.Equal("MenuFlyoutItem", items[3].Name.LocalName);
        Assert.Equal("MoreOpenManagerMenuItem", (string?)items[3].Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));

        Assert.Equal("MenuFlyoutItem", items[4].Name.LocalName);
        Assert.Equal("MoreSettingsMenuItem", (string?)items[4].Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));

        Assert.Equal("MenuFlyoutItem", items[5].Name.LocalName);
        Assert.Equal("MoreAboutMenuItem", (string?)items[5].Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
    }

    [Fact]
    public void QuickPanelWindowXamlConfiguresColorPickerAndPalette()
    {
        string appDir = FindAppDirectory();
        string xamlPath = Path.Combine(appDir, "QuickPanelWindow.xaml");
        string xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        var colorPicker = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ColorPicker" && (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CustomColorPicker");
        Assert.NotNull(colorPicker);

        Assert.Equal("False", (string?)colorPicker!.Attribute("IsAlphaEnabled"));
        Assert.Equal("True", (string?)colorPicker.Attribute("IsColorSpectrumVisible"));
        Assert.Equal("True", (string?)colorPicker.Attribute("IsColorSliderVisible"));
        Assert.Equal("True", (string?)colorPicker.Attribute("IsHexInputVisible"));
        Assert.Equal("True", (string?)colorPicker.Attribute("IsColorChannelTextInputVisible"));

        // 12 curated default colors
        Assert.Equal(12, BuiltInColors.Entries.Count);

        // 8 max recent colors
        Assert.Equal(8, UserSettings.MaxRecentCustomColors);
    }

    [Fact]
    public void ModeSeparationAndPlacementStagingWorks()
    {
        var staging = new PersonalizationStagingModel();
        staging.Initialize(PersonalizationMode.FolderTemplate);

        Assert.Equal(PersonalizationMode.FolderTemplate, staging.ActiveMode);
        Assert.False(staging.CanApply);

        // Stage custom image -> switches to Mode A
        byte[] fakeImageBytes = [1, 2, 3, 4, 5];
        staging.StageCustomImage(fakeImageBytes, "C:\\test\\photo.png", "hash123");

        Assert.Equal(PersonalizationMode.CustomImage, staging.ActiveMode);
        Assert.True(staging.CanApply);
        Assert.Equal(ImagePlacementMode.Contain, staging.StagedImageState.PlacementMode);

        // Stage placement parameters
        staging.StagePlacement(ImagePlacementMode.Cover, 12, BackgroundMode.SolidColor, "#123456");
        Assert.Equal(PersonalizationMode.CustomImage, staging.ActiveMode);
        Assert.Equal(ImagePlacementMode.Cover, staging.StagedImageState.PlacementMode);
        Assert.Equal(12, staging.StagedImageState.PaddingPercent);
        Assert.Equal(BackgroundMode.SolidColor, staging.StagedImageState.BackgroundMode);
        Assert.Equal("#123456", staging.StagedImageState.BackgroundColorHex);

        // Switch to folder template -> switches to Mode B
        staging.StageFolderTemplate("ornare-aqua", 2);
        Assert.Equal(PersonalizationMode.FolderTemplate, staging.ActiveMode);
        Assert.Equal("ornare-aqua", staging.StagedTemplateState.TemplateId);
        Assert.Equal(2, staging.StagedTemplateState.TemplateVersion);

        // Verify Mode A values retained in memory while in Mode B
        Assert.Equal(ImagePlacementMode.Cover, staging.StagedImageState.PlacementMode);
        Assert.Equal(12, staging.StagedImageState.PaddingPercent);

        // Stage color in Mode B
        staging.StageFolderColor("#DC3545");
        Assert.Equal(PersonalizationMode.FolderTemplate, staging.ActiveMode);
        Assert.Equal("#DC3545", staging.StagedTemplateState.FolderColorHex);

        // Commit stages
        staging.Commit();
        Assert.False(staging.CanApply);
        Assert.Equal(PersonalizationMode.FolderTemplate, staging.CommittedMode);
        Assert.Equal("#DC3545", staging.CommittedTemplateState.FolderColorHex);
    }

    [Fact]
    public void MoreMenuResourcesExistAcrossAllLocales()
    {
        string[] keys = ["More_ResetFolder", "More_OpenManager", "More_Settings", "More_About"];

        foreach (string loc in RuntimeResourceSource.CompleteLocales)
        {
            Dictionary<string, string> dictionary = RuntimeResourceSource.LoadLocale(loc);

            foreach (string key in keys)
            {
                Assert.True(dictionary.ContainsKey(key), $"Missing key '{key}' in {loc}");
                Assert.False(string.IsNullOrWhiteSpace(dictionary[key]), $"Empty value for '{key}' in {loc}");
            }
        }

        // English specific check
        Dictionary<string, string> enDict = RuntimeResourceSource.LoadLocale("en-US");
        Assert.Equal("Reset Folder Customization", enDict["More_ResetFolder"]);
        Assert.Equal("Open Manager", enDict["More_OpenManager"]);
        Assert.Equal("Settings", enDict["More_Settings"]);
        Assert.Equal("About / Open-source acknowledgements", enDict["More_About"]);

        // Turkish specific check
        Dictionary<string, string> trDict = RuntimeResourceSource.LoadLocale("tr-TR");
        Assert.Equal("Klasör Özelleştirmesini Sıfırla", trDict["More_ResetFolder"]);
        Assert.Equal("Yöneticiyi Aç", trDict["More_OpenManager"]);
        Assert.Equal("Ayarlar", trDict["More_Settings"]);
        Assert.Equal("Hakkında / Açık Kaynak Bildirimleri", trDict["More_About"]);
    }

    [Fact]
    public void QuickPanelWindowSourceImplementsSettingsAndAboutHandlers()
    {
        string appDir = FindAppDirectory();
        string csPath = Path.Combine(appDir, "QuickPanelWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"QuickPanelWindow.xaml.cs not found at {csPath}");

        string code = File.ReadAllText(csPath);

        Assert.Contains("void OnSettingsClicked", code);
        Assert.Contains("void OnAboutClicked", code);
        Assert.Contains("void OnFolderStyleFlyoutOpening", code);
        Assert.Contains("void OnIconFlyoutOpening", code);
        Assert.Contains("void OnEmojiFlyoutOpening", code);
        Assert.Contains("void OnCustomColorFlyoutOpened", code);
        Assert.Contains("void OnCustomColorFlyoutClosed", code);
        Assert.Contains("void OnApplyCustomColorClicked", code);
        Assert.Contains("void OnCancelCustomColorClicked", code);
    }
}
