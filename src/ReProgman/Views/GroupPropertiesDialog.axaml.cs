namespace ReProgman.Views;

public partial class GroupPropertiesDialog : Win31DialogWindow
{
    public string GroupName { get; private set; } = "";

    public GroupPropertiesDialog() : this("")
    {
    }

    public GroupPropertiesDialog(string initialName)
    {
        InitializeComponent();
        NameBox.Text = initialName;
        OkButton.Click += async (_, _) =>
        {
            var name = NameBox.Text?.Trim() ?? "";
            if (name.Length == 0)
            {
                await new MessageDialog(Strings.AppTitle, Strings.ErrEmptyDescription).ShowDialog(this);
                NameBox.Focus();
                return;
            }

            GroupName = name;
            Close(true);
        };
        CancelButton.Click += (_, _) => Close(false);
        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }
}
