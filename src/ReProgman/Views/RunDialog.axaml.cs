using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ReProgman.Views;

public partial class RunDialog : Win31DialogWindow
{
    public string? CommandLine { get; private set; }
    public bool RunMinimized { get; private set; }

    public RunDialog()
    {
        InitializeComponent();
        OkButton.Click += OnOkClicked;
        CancelButton.Click += (_, _) => Close(false);
        BrowseButton.Click += OnBrowseClicked;
        Opened += (_, _) => CommandBox.Focus();
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        var text = CommandBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            Close(false);
            return;
        }

        CommandLine = text;
        RunMinimized = MinimizedCheck.IsChecked == true;
        Close(true);
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
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
            CommandBox.Text = path.Contains(' ') ? $"\"{path}\"" : path;
        }
    }
}
