using HeartRate.Mobile.Views;

namespace HeartRate.Mobile;

public partial class AppShell : Shell
{
    public AppShell(SessionsPage sessionsPage)
    {
        InitializeComponent();
        Items.Add(new ShellContent
        {
            Route = "sessions",
            Content = sessionsPage
        });
    }
}
