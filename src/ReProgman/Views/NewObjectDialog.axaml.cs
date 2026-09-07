namespace ReProgman.Views;

public partial class NewObjectDialog : Win31DialogWindow
{
    public bool CreateGroup { get; private set; }

    public NewObjectDialog()
    {
        InitializeComponent();
        OkButton.Click += (_, _) =>
        {
            CreateGroup = GroupRadio.IsChecked == true;
            Close(true);
        };
        CancelButton.Click += (_, _) => Close(false);
    }
}
