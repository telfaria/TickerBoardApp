using System.Windows;
namespace TickerBoard;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(new JsonSettingsService(), new MockMarketDataProvider());
        MainWindow = window;
        window.Show();
    }
}
