namespace ReProgman.Views;

public partial class MoveCopyDialog : Win31DialogWindow
{
    public string? SelectedGroup { get; private set; }

    public MoveCopyDialog() : this("", "", [], isCopy: false)
    {
    }

    public MoveCopyDialog(string itemName, string fromGroup, IReadOnlyList<string> targetGroups, bool isCopy)
    {
        InitializeComponent();
        var title = isCopy ? Strings.CopyTitle : Strings.MoveTitle;
        Title = title;
        Shell.Title = title;
        ItemText.Text = Strings.ItemCaption(itemName);
        FromText.Text = Strings.FromGroupCaption(fromGroup);
        ToLabel.Text = isCopy ? Strings.CopyToLabel : Strings.MoveToLabel;
        GroupList.ItemsSource = targetGroups;
        if (targetGroups.Count > 0)
        {
            GroupList.SelectedIndex = 0;
        }

        GroupList.DoubleTapped += (_, _) => Accept();
        OkButton.Click += (_, _) => Accept();
        CancelButton.Click += (_, _) => Close(false);
    }

    private void Accept()
    {
        if (GroupList.SelectedItem is string group)
        {
            SelectedGroup = group;
            Close(true);
        }
    }
}
