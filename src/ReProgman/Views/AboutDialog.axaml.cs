namespace ReProgman.Views;

public partial class AboutDialog : Win31DialogWindow
{
    public AboutDialog()
    {
        InitializeComponent();
        OkButton.Click += (_, _) => Close();
    }
}
