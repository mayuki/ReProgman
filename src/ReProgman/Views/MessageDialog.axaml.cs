namespace ReProgman.Views;

public partial class MessageDialog : Win31DialogWindow
{
    public MessageDialog() : this(Strings.AppTitle, "")
    {
    }

    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        Shell.Title = title;
        MessageText.Text = message;
        OkButton.Click += (_, _) => Close();
    }
}
