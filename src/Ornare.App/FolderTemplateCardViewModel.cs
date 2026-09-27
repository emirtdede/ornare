using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace Ornare.App;

/// <summary>
/// ViewModel for items in the 3x3 Folder Style flyout grid (Section 27).
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class FolderTemplateCardViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private ImageSource? _thumbnail;
    private Brush? _borderBrush;
    private Brush? _background;

    public string Id { get; }
    public int Version { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            OnPropertyChanged();
        }
    }

    public Brush? BorderBrush
    {
        get => _borderBrush;
        set
        {
            _borderBrush = value;
            OnPropertyChanged();
        }
    }

    public Brush? Background
    {
        get => _background;
        set
        {
            _background = value;
            OnPropertyChanged();
        }
    }

    public FolderTemplateCardViewModel(string id, int version, string displayName, int sortOrder)
    {
        Id = id;
        Version = version;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
