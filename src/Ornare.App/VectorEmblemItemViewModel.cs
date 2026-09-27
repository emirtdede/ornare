using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Ornare.App;

/// <summary>
/// ViewModel for vector emblems displayed in the IconFlyout GridView (Section 18 &amp; 28).
/// </summary>
public sealed class VectorEmblemItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private Brush? _borderBrush;
    private Brush? _background;
    private Geometry? _geometry;

    public string Id { get; }
    public string ShortId { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string PathData { get; }

    public Geometry? GeometryData
    {
        get
        {
            if (_geometry == null && !string.IsNullOrEmpty(PathData))
            {
                try
                {
                    _geometry = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), PathData);
                }
                catch
                {
                    // Fallback to empty geometry on malformed path
                    _geometry = null;
                }
            }
            return _geometry;
        }
    }

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

    public Brush? BorderBrush
    {
        get => _borderBrush;
        set
        {
            if (_borderBrush != value)
            {
                _borderBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public Brush? Background
    {
        get => _background;
        set
        {
            if (_background != value)
            {
                _background = value;
                OnPropertyChanged();
            }
        }
    }

    public VectorEmblemItemViewModel(
        string id,
        string shortId,
        string displayName,
        string category,
        string pathData)
    {
        Id = id;
        ShortId = shortId;
        DisplayName = displayName;
        Category = category;
        PathData = pathData;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
