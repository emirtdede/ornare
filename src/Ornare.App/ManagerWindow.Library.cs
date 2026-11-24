namespace Ornare.App;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ornare.Core;
using Ornare.Storage;

/// <summary>
/// Library tag management: the tag chip list, assignment, bulk tagging and removal.
/// <para>
/// Extracted so the tag flow reads on its own instead of filling the middle of the window
/// class. Every method is a member of the same partial class.
/// </para>
/// </summary>
public sealed partial class ManagerWindow
{
    /// <summary>
    /// One-shot informational dialog. Centralised so a new failure path cannot invent its own
    /// dialog shape, and so the close button is always localised.
    /// </summary>
    private async Task ShowAlertAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = L("Close"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    #region F15: Library Tags UI

    private async Task RefreshTagFilterBarAsync()
    {
        if (_storageService == null) return;

        var tags = await _storageService.GetAllTagsAsync();
        if (tags.Count == 0 && string.IsNullOrEmpty(_selectedFilterTagId))
        {
            TagFilterScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        TagFilterScrollViewer.Visibility = Visibility.Visible;
        TagFilterBar.Children.Clear();

        string allLabel = L("Category_All");

        // "All" pill
        var allBtn = CreateFilterPill(allLabel, null, _selectedFilterTagId == null);
        TagFilterBar.Children.Add(allBtn);

        foreach (var tag in tags)
        {
            var btn = CreateFilterPill(tag.DisplayName, tag.Id, _selectedFilterTagId == tag.Id);
            TagFilterBar.Children.Add(btn);
        }
    }

    private Button CreateFilterPill(string label, string? tagId, bool isSelected)
    {
        var btn = new Button
        {
            Content = label,
            Tag = tagId,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 12,
            FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
        };

        if (isSelected)
        {
            if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accentBrush))
            {
                btn.Background = (Brush)accentBrush;
            }
            if (Application.Current.Resources.TryGetValue("TextOnAccentFillColorPrimaryBrush", out object? textAccentBrush))
            {
                btn.Foreground = (Brush)textAccentBrush;
            }
        }
        else
        {
            if (Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out object? subtleBrush))
            {
                btn.Background = (Brush)subtleBrush;
            }
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? textBrush))
            {
                btn.Foreground = (Brush)textBrush;
            }
        }

        btn.Click += async (s, e) => await GuardAsync("ApplyTagFilter", async () =>
        {
            _selectedFilterTagId = tagId;
            await LoadLibraryAsync();
        });
        return btn;
    }

    private async void OnManageTagsClicked(object sender, RoutedEventArgs e)
    {
        if (_storageService == null) return;
        string? sha256 = null;
        if (sender is MenuFlyoutItem mfi && mfi.Tag is string s) sha256 = s;
        if (string.IsNullOrEmpty(sha256)) return;

        var dialog = new ContentDialog
        {
            Title = L("ManageTags_Title"),
            CloseButtonText = L("Close"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var rootPanel = new StackPanel { Spacing = 12, Width = 360 };

        var tagsWrap = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var tagsScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = tagsWrap
        };

        var noTagsText = new TextBlock
        {
            Text = L("ManageTags_Empty"),
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secTextBrush))
        {
            noTagsText.Foreground = (Brush)secTextBrush;
        }

        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox
        {
            PlaceholderText = L("Tag_AddPlaceholder"),
            MaxLength = 40
        };
        Grid.SetColumn(textBox, 0);

        var addBtn = new Button
        {
            Content = L("Add"),
            Margin = new Thickness(8, 0, 0, 0)
        };
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out object? accentStyle))
        {
            addBtn.Style = (Style)accentStyle;
        }
        Grid.SetColumn(addBtn, 1);

        inputGrid.Children.Add(textBox);
        inputGrid.Children.Add(addBtn);

        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 239, 68, 68)),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap
        };

        rootPanel.Children.Add(tagsScroll);
        rootPanel.Children.Add(noTagsText);
        rootPanel.Children.Add(inputGrid);
        rootPanel.Children.Add(errorText);
        dialog.Content = rootPanel;

        async Task RefreshDialogTagsAsync()
        {
            tagsWrap.Children.Clear();
            var currentTags = await _storageService.GetTagsForAssetAsync(sha256);
            if (currentTags.Count == 0)
            {
                noTagsText.Visibility = Visibility.Visible;
                tagsScroll.Visibility = Visibility.Collapsed;
            }
            else
            {
                noTagsText.Visibility = Visibility.Collapsed;
                tagsScroll.Visibility = Visibility.Visible;
                foreach (var t in currentTags)
                {
                    var pill = new Border
                    {
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 3, 4, 3),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 4, 4)
                    };
                    if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? cardBg))
                    {
                        pill.Background = (Brush)cardBg;
                    }
                    if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? cardStroke))
                    {
                        pill.BorderBrush = (Brush)cardStroke;
                    }

                    var pillStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    pillStack.Children.Add(new TextBlock { Text = t.DisplayName, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                    var removeBtn = new Button
                    {
                        Padding = new Thickness(0),
                        Width = 20,
                        Height = 20,
                        CornerRadius = new CornerRadius(10),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Content = new FontIcon { Glyph = "\uE711", FontSize = 10 }
                    };
                    removeBtn.Click += async (s, args) => await GuardAsync("DetachTag", async () =>
                    {
                        await _storageService.DetachTagFromAssetAsync(sha256, t.Id);
                        await RefreshDialogTagsAsync();
                        await RefreshTagFilterBarAsync();
                    });
                    pillStack.Children.Add(removeBtn);
                    pill.Child = pillStack;
                    tagsWrap.Children.Add(pill);
                }
            }
        }

        async Task TryAddTagAsync()
        {
            errorText.Visibility = Visibility.Collapsed;
            string raw = textBox.Text;
            if (!LibraryNameNormalizer.TryValidateAndNormalizeDetailed(raw, out string display, out _, out LibraryNameError nameError))
            {
                errorText.Text = LocalizedNameError(nameError);
                errorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var tag = await _storageService.CreateOrGetTagAsync(display);
                await _storageService.AttachTagToAssetAsync(sha256, tag.Id);
                textBox.Text = string.Empty;
                await RefreshDialogTagsAsync();
                await RefreshTagFilterBarAsync();
            }
            catch (Exception ex)
            {
                ShowDialogError(errorText, ex, "AddTag");
            }
        }

        addBtn.Click += async (s, args) => await GuardAsync("AddTag", TryAddTagAsync);
        textBox.KeyDown += async (s, args) =>
        {
            if (args.Key == global::Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                await GuardAsync("AddTag", TryAddTagAsync);
            }
        };

        await RefreshDialogTagsAsync();
        await dialog.ShowAsync();
    }

    #endregion
}