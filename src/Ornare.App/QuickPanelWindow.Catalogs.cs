namespace Ornare.App;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ornare.Core;
using Ornare.Imaging.Emblems;

/// <summary>
/// Emblem and emoji catalog: initialisation, filtering, selection and emoji/text insertion.
/// <para>
/// Both catalogs share the same shape (load once, filter on search and category, paint the
/// selected card, stage the choice), so they live together and away from the window class.
/// The two copies are deliberately parallel rather than merged behind an interface: they
/// have different item types and different staging calls, and a generic abstraction here
/// would be larger than the duplication it removes.
/// </para>
/// </summary>
public sealed partial class QuickPanelWindow
{
    private void EnsureEmblemCatalogInitialized()
    {
        if (_emblemCatalogInitialized)
        {
            return;
        }

        try
        {
            var allEntries = VectorEmblemCatalog.All;
            _allEmblemCards = new List<VectorEmblemItemViewModel>(allEntries.Count);
            foreach (var entry in allEntries)
            {
                _allEmblemCards.Add(new VectorEmblemItemViewModel(
                    entry.Id,
                    entry.ShortId,
                    entry.DisplayName,
                    entry.Category,
                    entry.PathData));
            }

            if (IconCategoryComboBox != null)
            {
                IconCategoryComboBox.Items.Clear();
                IconCategoryComboBox.Items.Add(AppResources.GetString("Category_All", _currentLanguage));
                foreach (var cat in VectorEmblemCatalog.Categories)
                {
                    IconCategoryComboBox.Items.Add(cat);
                }
                IconCategoryComboBox.SelectedIndex = 0;
            }

            VectorEmblemCards.Clear();
            foreach (var card in _allEmblemCards)
            {
                UpdateEmblemCardVisualState(card);
                VectorEmblemCards.Add(card);
            }

            if (IconGridView != null && IconGridView.ItemsSource == null)
            {
                IconGridView.ItemsSource = VectorEmblemCards;
            }

            _emblemCatalogInitialized = true;
        }
        catch (Exception ex)
        {
            App.Log($"Failed to initialize vector emblem catalog: {ex}");
        }
    }

    private void SyncEmblemSelectionState()
    {
        var currentEmblem = _stagingModel.StagedTemplateState.Emblem;
        bool hasVectorEmblem = currentEmblem.Kind == EmblemKind.VectorIcon && !string.IsNullOrWhiteSpace(currentEmblem.ContentId);

        if (RemoveEmblemButton != null)
        {
            RemoveEmblemButton.Visibility = hasVectorEmblem ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_allEmblemCards != null)
        {
            foreach (var card in _allEmblemCards)
            {
                card.IsSelected = hasVectorEmblem && (
                    string.Equals(card.Id, currentEmblem.ContentId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(card.ShortId, currentEmblem.ContentId, StringComparison.OrdinalIgnoreCase));
                UpdateEmblemCardVisualState(card);
            }
        }
    }

    private static void UpdateEmblemCardVisualState(VectorEmblemItemViewModel card)
    {
        if (card.IsSelected)
        {
            card.BorderBrush = GetThemeBrush("AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
            card.Background = GetThemeBrush("SubtleFillColorSecondaryBrush", Microsoft.UI.Colors.Transparent);
        }
        else
        {
            card.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            card.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void OnIconSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterEmblemCards();
    }

    private void OnIconCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterEmblemCards();
    }

    private void FilterEmblemCards()
    {
        if (_allEmblemCards == null)
        {
            return;
        }

        string query = IconSearchTextBox?.Text?.Trim() ?? string.Empty;
        string? selectedCat = IconCategoryComboBox?.SelectedItem as string;
        string allLabel = AppResources.GetString("Category_All", _currentLanguage);
        if (string.Equals(selectedCat, allLabel, StringComparison.OrdinalIgnoreCase) || string.Equals(selectedCat, "All", StringComparison.OrdinalIgnoreCase))
        {
            selectedCat = null;
        }

        var filtered = VectorEmblemCatalog.Search(query, selectedCat);
        var filteredIds = new HashSet<string>(filtered.Select(f => f.Id), StringComparer.OrdinalIgnoreCase);

        VectorEmblemCards.Clear();
        foreach (var card in _allEmblemCards)
        {
            if (filteredIds.Contains(card.Id))
            {
                UpdateEmblemCardVisualState(card);
                VectorEmblemCards.Add(card);
            }
        }
    }

    private async void OnIconGridItemClick(object sender, ItemClickEventArgs e)
    {
        await GuardAsync(nameof(OnIconGridItemClick), async () =>
        {
            if (e.ClickedItem is VectorEmblemItemViewModel card)
            {
                _stagingModel.StageVectorEmblem(card.Id, _selectedEmblemColorHex);
                SyncEmblemSelectionState();
                IconFlyout?.Hide();
                await RefreshLivePreviewAsync();
            }
        });
    }

    private async void OnRemoveEmblemClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnRemoveEmblemClicked), async () =>
        {
            _stagingModel.RemoveEmblem();
            SyncEmblemSelectionState();
            SyncEmojiSelectionState();
            IconFlyout?.Hide();
            EmojiFlyout?.Hide();
            await RefreshLivePreviewAsync();
        });
    }

    private void EnsureEmojiCatalogInitialized()
    {
        if (_emojiCatalogInitialized)
        {
            return;
        }

        try
        {
            var allEntries = EmojiCatalog.All;
            _allEmojiCards = new List<EmojiItemViewModel>(allEntries.Count);
            foreach (var entry in allEntries)
            {
                _allEmojiCards.Add(new EmojiItemViewModel(
                    entry.Grapheme,
                    entry.Name,
                    entry.Category));
            }

            if (EmojiCategoryComboBox != null)
            {
                EmojiCategoryComboBox.Items.Clear();
                EmojiCategoryComboBox.Items.Add(AppResources.GetString("Category_All", _currentLanguage));
                foreach (var cat in EmojiCatalog.Categories)
                {
                    EmojiCategoryComboBox.Items.Add(cat);
                }
                EmojiCategoryComboBox.SelectedIndex = 0;
            }

            EmojiCards.Clear();
            foreach (var card in _allEmojiCards)
            {
                UpdateEmojiCardVisualState(card);
                EmojiCards.Add(card);
            }

            if (EmojiGridView != null && EmojiGridView.ItemsSource == null)
            {
                EmojiGridView.ItemsSource = EmojiCards;
            }

            _emojiCatalogInitialized = true;
        }
        catch (Exception ex)
        {
            App.Log($"Failed to initialize emoji catalog: {ex}");
        }
    }

    private void SyncEmojiSelectionState()
    {
        var currentEmblem = _stagingModel.StagedTemplateState.Emblem;
        bool hasEmojiEmblem = currentEmblem.Kind == EmblemKind.Emoji && !string.IsNullOrWhiteSpace(currentEmblem.GraphemeSequence);

        if (RemoveEmojiEmblemButton != null)
        {
            RemoveEmojiEmblemButton.Visibility = hasEmojiEmblem ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_allEmojiCards != null)
        {
            foreach (var card in _allEmojiCards)
            {
                card.IsSelected = hasEmojiEmblem && string.Equals(card.Grapheme, currentEmblem.GraphemeSequence, StringComparison.Ordinal);
                UpdateEmojiCardVisualState(card);
            }
        }
    }

    private static void UpdateEmojiCardVisualState(EmojiItemViewModel card)
    {
        if (card.IsSelected)
        {
            card.BorderBrush = GetThemeBrush("AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
            card.Background = GetThemeBrush("SubtleFillColorSecondaryBrush", Microsoft.UI.Colors.Transparent);
        }
        else
        {
            card.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            card.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void OnEmojiSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterEmojiCards();
    }

    private void OnEmojiCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterEmojiCards();
    }

    private void FilterEmojiCards()
    {
        if (_allEmojiCards == null)
        {
            return;
        }

        string query = EmojiSearchTextBox?.Text?.Trim() ?? string.Empty;
        string? selectedCat = EmojiCategoryComboBox?.SelectedItem as string;
        string allLabel = AppResources.GetString("Category_All", _currentLanguage);
        if (string.Equals(selectedCat, allLabel, StringComparison.OrdinalIgnoreCase) || string.Equals(selectedCat, "All", StringComparison.OrdinalIgnoreCase))
        {
            selectedCat = null;
        }

        var filtered = EmojiCatalog.Search(query, selectedCat);
        var filteredGraphemes = new HashSet<string>(filtered.Select(f => f.Grapheme), StringComparer.Ordinal);

        EmojiCards.Clear();
        foreach (var card in _allEmojiCards)
        {
            if (filteredGraphemes.Contains(card.Grapheme))
            {
                UpdateEmojiCardVisualState(card);
                EmojiCards.Add(card);
            }
        }
    }

    private async void OnEmojiGridItemClick(object sender, ItemClickEventArgs e)
    {
        await GuardAsync(nameof(OnEmojiGridItemClick), async () =>
        {
            if (e.ClickedItem is EmojiItemViewModel card)
            {
                _stagingModel.StageEmojiEmblem(card.Grapheme);
                SyncEmojiSelectionState();
                SyncEmblemSelectionState();
                EmojiFlyout?.Hide();
                await RefreshLivePreviewAsync();
            }
        });
    }
}