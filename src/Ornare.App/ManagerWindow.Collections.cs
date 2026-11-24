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
/// Custom collections: list, create, rename, reorder, delete and manage membership.
/// <para>
/// Extracted from the window class so the collection flow reads on its own instead of sitting
/// between the tags region and the presets region. Every method here is a member of the same
/// partial class and shares the window's storage service and localized copy helper.
/// </para>
/// </summary>
public sealed partial class ManagerWindow
{
    #region F16: Custom Collections UI

    private string? _selectedCollectionId;
    private readonly List<CollectionItemVm> _collectionItems = new();
    private readonly List<LibraryItemVm> _currentCollectionAssets = new();

    private async Task LoadCollectionsViewAsync()
    {
        if (_storageService == null || CollectionsListView == null) return;

        var collections = await _storageService.GetAllCollectionsAsync();
        _collectionItems.Clear();

        foreach (var c in collections)
        {
            var assets = await _storageService.GetAssetsInCollectionAsync(c.Id, limit: 1000);
            int count = assets.Count;
            string countText = count == 1
                ? L("Item_Icons_Single")
                : AppResources.Format("Item_Icons_Count", _currentLanguage, count);

            _collectionItems.Add(new CollectionItemVm
            {
                Id = c.Id,
                DisplayName = c.DisplayName,
                SortKey = c.SortKey,
                AssetCountText = countText,
                MoveUpText = L("Collection_MoveUp"),
                MoveDownText = L("Collection_MoveDown"),
                RenameText = L("Collection_Rename"),
                DeleteText = L("Delete")
            });
        }

        SyncItems(CollectionsListView, _collectionItems);

        if (CollectionsEmptyPanel != null)
            CollectionsEmptyPanel.Visibility = _collectionItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CollectionsListView != null)
            CollectionsListView.Visibility = _collectionItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_collectionItems.Count > 0)
        {
            if (string.IsNullOrEmpty(_selectedCollectionId) || !_collectionItems.Any(ci => ci.Id == _selectedCollectionId))
            {
                _selectedCollectionId = _collectionItems[0].Id;
                if (CollectionsListView != null) CollectionsListView.SelectedIndex = 0;
            }
            else
            {
                int idx = _collectionItems.FindIndex(ci => ci.Id == _selectedCollectionId);
                if (idx >= 0 && CollectionsListView != null) CollectionsListView.SelectedIndex = idx;
            }

            if (!string.IsNullOrEmpty(_selectedCollectionId))
            {
                await LoadCollectionAssetsAsync(_selectedCollectionId);
            }
        }
        else
        {
            _selectedCollectionId = null;
            if (SelectedCollectionTitle != null) SelectedCollectionTitle.Text = string.Empty;
            if (SelectedCollectionCount != null) SelectedCollectionCount.Text = string.Empty;
            _currentCollectionAssets.Clear();
            if (CollectionAssetsGridView != null) SyncItems(CollectionAssetsGridView, _currentCollectionAssets);
            if (CollectionAssetsEmptyPanel != null) CollectionAssetsEmptyPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadCollectionAssetsAsync(string collectionId)
    {
        if (_storageService == null || CollectionAssetsGridView == null) return;

        var selectedColl = await _storageService.GetCollectionByIdAsync(collectionId);
        if (selectedColl != null)
        {
            if (SelectedCollectionTitle != null) SelectedCollectionTitle.Text = selectedColl.DisplayName;
        }

        var assets = await _storageService.GetAssetsInCollectionAsync(collectionId, limit: 200);
        int count = assets.Count;
        if (SelectedCollectionCount != null)
        {
            SelectedCollectionCount.Text = count == 1
                ? $"({L("Item_Icons_Single")})"
                : AppResources.Format("SelectedCollectionCount", _currentLanguage, count);
        }

        _currentCollectionAssets.Clear();
        foreach (var asset in assets)
        {
            _currentCollectionAssets.Add(CreateLibraryVm(asset, asset.IsFavorite));
        }

        SyncItems(CollectionAssetsGridView, _currentCollectionAssets);

        if (CollectionAssetsEmptyPanel != null)
            CollectionAssetsEmptyPanel.Visibility = _currentCollectionAssets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CollectionAssetsGridView != null)
            CollectionAssetsGridView.Visibility = _currentCollectionAssets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnCollectionsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await GuardAsync(nameof(OnCollectionsListViewSelectionChanged), async () =>
        {
            if (CollectionsListView?.SelectedItem is CollectionItemVm item)
            {
                _selectedCollectionId = item.Id;
                await LoadCollectionAssetsAsync(item.Id);
            }
        });
    }

    private async void OnNewCollectionClicked(object sender, RoutedEventArgs e)
    {
        if (_storageService == null || Content?.XamlRoot == null) return;

        var textBox = new TextBox
        {
            PlaceholderText = L("Collection_NamePlaceholder"),
            MaxLength = 80
        };

        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 239, 68, 68)),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        var content = new StackPanel { Spacing = 8, Width = 320 };
        content.Children.Add(textBox);
        content.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = L("Collections_NewCollectionTitle"),
            PrimaryButtonText = L("Create"),
            CloseButtonText = L("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
            Content = content
        };

        dialog.PrimaryButtonClick += async (d, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (!LibraryNameNormalizer.TryValidateAndNormalizeDetailed(
                    textBox.Text,
                    out string displayName,
                    out _,
                    out LibraryNameError nameError,
                    maxScalars: LibraryNameNormalizer.MaxScalarsPerCollection))
                {
                    args.Cancel = true;
                    errorText.Text = LocalizedNameError(nameError);
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                var created = await _storageService.CreateCollectionAsync(displayName);
                _selectedCollectionId = created.Id;
                await LoadCollectionsViewAsync();
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                ShowDialogError(errorText, ex, "CollectionDialog");
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
    }

    private void OnRenameCollectionClicked(object sender, RoutedEventArgs e)
    {
        _ = GuardAsync(nameof(OnRenameCollectionClicked), () => RenameCollectionCoreAsync(sender, e));
    }

    private async Task RenameCollectionCoreAsync(object sender, RoutedEventArgs e)
    {
        if (_storageService == null || Content?.XamlRoot == null) return;
        if (sender is not Button btn || btn.Tag is not string collectionId) return;

        var collection = await _storageService.GetCollectionByIdAsync(collectionId);
        if (collection == null) return;

        var textBox = new TextBox
        {
            Text = collection.DisplayName,
            MaxLength = 80
        };

        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 239, 68, 68)),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        var content = new StackPanel { Spacing = 8, Width = 320 };
        content.Children.Add(textBox);
        content.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = L("Collections_RenameTitle"),
            PrimaryButtonText = L("Save"),
            CloseButtonText = L("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
            Content = content
        };

        dialog.PrimaryButtonClick += async (d, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (!LibraryNameNormalizer.TryValidateAndNormalizeDetailed(
                    textBox.Text,
                    out string displayName,
                    out _,
                    out LibraryNameError nameError,
                    maxScalars: LibraryNameNormalizer.MaxScalarsPerCollection))
                {
                    args.Cancel = true;
                    errorText.Text = LocalizedNameError(nameError);
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                await _storageService.RenameCollectionAsync(collectionId, displayName);
                await LoadCollectionsViewAsync();
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                ShowDialogError(errorText, ex, "RenameCollection");
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
    }

    private async void OnDeleteCollectionClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnDeleteCollectionClicked), async () =>
        {
            if (_storageService == null || Content?.XamlRoot == null) return;
            if (sender is not Button btn || btn.Tag is not string collectionId) return;

            var dialog = new ContentDialog
            {
                Title = AppResources.GetString("Collections_DeleteTitle", _currentLanguage),
                Content = AppResources.GetString("Collections_DeleteConfirm", _currentLanguage),
                PrimaryButtonText = AppResources.GetString("Delete", _currentLanguage),
                CloseButtonText = AppResources.GetString("Cancel", _currentLanguage),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _storageService.DeleteCollectionAsync(collectionId);
                if (_selectedCollectionId == collectionId) _selectedCollectionId = null;
                await LoadCollectionsViewAsync();
            }
        });
    }

    private async void OnMoveCollectionUpClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnMoveCollectionUpClicked), async () =>
        {
            if (_storageService == null) return;
            if (sender is not Button btn || btn.Tag is not string collectionId) return;

            int idx = _collectionItems.FindIndex(c => c.Id == collectionId);
            if (idx > 0)
            {
                string targetId = _collectionItems[idx - 1].Id;
                await _storageService.ReorderCollectionAsync(collectionId, targetId, insertBefore: true);
                await LoadCollectionsViewAsync();
            }
        });
    }

    private async void OnMoveCollectionDownClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnMoveCollectionDownClicked), async () =>
        {
            if (_storageService == null) return;
            if (sender is not Button btn || btn.Tag is not string collectionId) return;

            int idx = _collectionItems.FindIndex(c => c.Id == collectionId);
            if (idx >= 0 && idx < _collectionItems.Count - 1)
            {
                string targetId = _collectionItems[idx + 1].Id;
                await _storageService.ReorderCollectionAsync(collectionId, targetId, insertBefore: false);
                await LoadCollectionsViewAsync();
            }
        });
    }

    private async void OnRemoveFromCurrentCollectionClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnRemoveFromCurrentCollectionClicked), async () =>
        {
            if (_storageService == null || string.IsNullOrEmpty(_selectedCollectionId)) return;
            if (sender is not MenuFlyoutItem item || item.Tag is not string sha256) return;

            await _storageService.RemoveAssetFromCollectionAsync(_selectedCollectionId, sha256);
            await LoadCollectionAssetsAsync(_selectedCollectionId);

            var ci = _collectionItems.FirstOrDefault(c => c.Id == _selectedCollectionId);
            if (ci != null)
            {
                var assets = await _storageService.GetAssetsInCollectionAsync(_selectedCollectionId, limit: 1000);
                ci.AssetCountText = string.Format(
                    AppResources.CultureFor(_currentLanguage),
                    AppResources.GetString("Collection_AssetCount", _currentLanguage),
                    assets.Count);
                SyncItems(CollectionsListView, _collectionItems);
            }
        });
    }

    private async void OnManageCollectionsClicked(object sender, RoutedEventArgs e)
    {
        if (_storageService == null) return;
        string? sha256 = null;
        if (sender is MenuFlyoutItem mfi && mfi.Tag is string s) sha256 = s;
        if (string.IsNullOrEmpty(sha256) || Content?.XamlRoot == null) return;

        var dialog = new ContentDialog
        {
            Title = L("ManageCollections_Title"),
            CloseButtonText = L("Close"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var rootPanel = new StackPanel { Spacing = 12, Width = 360 };

        var collectionsListPanel = new StackPanel { Spacing = 6 };
        var collectionsScroll = new ScrollViewer
        {
            MaxHeight = 240,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = collectionsListPanel
        };

        var noCollectionsText = new TextBlock
        {
            Text = L("Collections_Empty"),
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secTextBrush))
        {
            noCollectionsText.Foreground = (Brush)secTextBrush;
        }

        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox
        {
            PlaceholderText = L("Collection_NewPlaceholder"),
            MaxLength = 80
        };
        Grid.SetColumn(textBox, 0);

        var addBtn = new Button
        {
            Content = L("Create"),
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

        rootPanel.Children.Add(collectionsScroll);
        rootPanel.Children.Add(noCollectionsText);
        rootPanel.Children.Add(inputGrid);
        rootPanel.Children.Add(errorText);
        dialog.Content = rootPanel;

        async Task RefreshCollectionsInDialogAsync()
        {
            collectionsListPanel.Children.Clear();
            var allCollections = await _storageService.GetAllCollectionsAsync();
            var assetCollections = await _storageService.GetCollectionsForAssetAsync(sha256);
            var memberSet = new HashSet<string>(assetCollections.Select(c => c.Id), StringComparer.Ordinal);

            if (allCollections.Count == 0)
            {
                noCollectionsText.Visibility = Visibility.Visible;
            }
            else
            {
                noCollectionsText.Visibility = Visibility.Collapsed;
                foreach (var c in allCollections)
                {
                    var cb = new CheckBox
                    {
                        Content = c.DisplayName,
                        IsChecked = memberSet.Contains(c.Id),
                        Tag = c.Id
                    };
                    cb.Click += async (s, args) => await GuardAsync("ToggleCollectionMembership", async () =>
                    {
                        if (cb.IsChecked == true)
                        {
                            await _storageService.AddAssetToCollectionAsync(c.Id, sha256);
                        }
                        else
                        {
                            await _storageService.RemoveAssetFromCollectionAsync(c.Id, sha256);
                        }

                        if (CollectionsView != null && CollectionsView.Visibility == Visibility.Visible)
                        {
                            await LoadCollectionsViewAsync();
                        }
                    });
                    collectionsListPanel.Children.Add(cb);
                }
            }
        }

        async Task TryCreateCollectionAsync()
        {
            errorText.Visibility = Visibility.Collapsed;
            string text = textBox.Text;
            if (!LibraryNameNormalizer.TryValidateAndNormalizeDetailed(text, out string display, out _, out LibraryNameError nameError, maxScalars: LibraryNameNormalizer.MaxScalarsPerCollection))
            {
                errorText.Text = LocalizedNameError(nameError);
                errorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var newColl = await _storageService.CreateCollectionAsync(display);
                await _storageService.AddAssetToCollectionAsync(newColl.Id, sha256);
                textBox.Text = string.Empty;
                await RefreshCollectionsInDialogAsync();
                if (CollectionsView != null && CollectionsView.Visibility == Visibility.Visible)
                {
                    await LoadCollectionsViewAsync();
                }
            }
            catch (Exception ex)
            {
                ShowDialogError(errorText, ex, "AddAssetToCollection");
            }
        }

        addBtn.Click += async (s, args) => await GuardAsync("CreateCollection", TryCreateCollectionAsync);
        textBox.KeyDown += async (s, args) =>
        {
            if (args.Key == global::Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                await GuardAsync("CreateCollection", TryCreateCollectionAsync);
            }
        };

        await RefreshCollectionsInDialogAsync();
        await dialog.ShowAsync();
    }

    #endregion
}