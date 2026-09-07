namespace ReProgman.Views;

public partial class ConfirmDialog : Win31DialogWindow
{
    public ConfirmDialog() : this(Strings.AppTitle, "")
    {
    }

    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        Shell.Title = title;
        MessageText.Text = message;
        YesButton.Click += (_, _) => Close(true);
        NoButton.Click += (_, _) => Close(false);
    }
}
