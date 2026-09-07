using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using ReProgman.Model;

namespace ReProgman.ViewModels;

public sealed class GroupViewModel(ProgramGroup group) : INotifyPropertyChanged
{
    private string _name = group.Name;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }

    public ObservableCollection<ItemViewModel> Items { get; } =
        new(group.Items.Select(i => new ItemViewModel(i)));

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProgramGroup ToModel() => new(Name, Items.Select(i => new ProgramItem(i.Name, i.Path)).ToArray());
}

public sealed class ItemViewModel(ProgramItem item) : INotifyPropertyChanged
{
    private Bitmap? _icon;
    private double _iconX;
    private double _iconY;
    private string _name = item.Name;
    private string _path = item.Path;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Icon top-left inside the group window. Unplaced items get a grid cell on first layout.</summary>
    public double IconX => _iconX;

    public double IconY => _iconY;

    public bool IsPlaced { get; private set; }

    /// <summary>Forgets the stored position so the next layout assigns a free grid cell.</summary>
    public void ClearPosition() => IsPlaced = false;

    public void SetPosition(double x, double y)
    {
        IsPlaced = true;
        if (_iconX != x)
        {
            _iconX = x;
            OnPropertyChanged(nameof(IconX));
        }

        if (_iconY != y)
        {
            _iconY = y;
            OnPropertyChanged(nameof(IconY));
        }
    }

    public Bitmap? Icon
    {
        get => _icon;
        set
        {
            if (!ReferenceEquals(_icon, value))
            {
                _icon = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasIcon));
            }
        }
    }

    public bool HasIcon => _icon is not null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
