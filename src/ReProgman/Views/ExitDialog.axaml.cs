namespace ReProgman.Views;

public partial class ExitDialog : Win31DialogWindow
{
    public ExitDialog()
    {
        InitializeComponent();
        OkButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
        // Keyboard-style focus so OK shows the dotted focus rectangle, like the original.
        Opened += (_, _) => OkButton.Focus(Avalonia.Input.NavigationMethod.Tab);
    }
}
