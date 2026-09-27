using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace Ornare.App;

/// <summary>
/// ViewModel for Unicode emoji entries displayed in the EmojiFlyout GridView (Section 24 &amp; 29).
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class EmojiItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private Brush? _borderBrush;
    private Brush? _background;

    public string Grapheme { get; }
    public string Name { get; }
    public string Category { get; }

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
            if (!Equals(_borderBrush, value))
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
            if (!Equals(_background, value))
            {
                _background = value;
                OnPropertyChanged();
            }
        }
    }

    public EmojiItemViewModel(string grapheme, string name, string category)
    {
        Grapheme = grapheme;
        Name = name;
        Category = category;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
