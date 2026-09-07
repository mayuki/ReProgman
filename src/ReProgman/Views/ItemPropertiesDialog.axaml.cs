using Avalonia.Platform.Storage;

namespace ReProgman.Views;

public partial class ItemPropertiesDialog : Win31DialogWindow
{
    public string ItemName { get; private set; } = "";
    public string ItemPath { get; private set; } = "";

    public ItemPropertiesDialog() : this("", "")
    {
    }

    public ItemPropertiesDialog(string name, string path)
    {
        InitializeComponent();
        NameBox.Text = name;
        PathBox.Text = path;
        OkButton.Click += async (_, _) =>
        {
            var newName = NameBox.Text?.Trim() ?? "";
            if (newName.Length == 0)
            {
                await new MessageDialog(Strings.AppTitle, Strings.ErrEmptyDescription).ShowDialog(this);
                NameBox.Focus();
                return;
            }

            var newPath = PathBox.Text?.Trim().Trim('"') ?? "";
            if (newPath.Length == 0)
            {
                PathBox.Focus();
                return;
            }

            ItemName = newName;
            ItemPath = newPath;
            Close(true);
        };
        CancelButton.Click += (_, _) => Close(false);
        BrowseButton.Click += OnBrowseClicked;
        Opened += (_, _) => NameBox.Focus();
    }

    private async void OnBrowseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.BrowseTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.FilterPrograms) { Patterns = ["*.exe", "*.pif", "*.com", "*.bat", "*.lnk"] },
                new FilePickerFileType(Strings.FilterAllFiles) { Patterns = ["*.*"] },
            ],
        });

        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path)
        {
            PathBox.Text = path;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(path);
            }
        }
    }
}
