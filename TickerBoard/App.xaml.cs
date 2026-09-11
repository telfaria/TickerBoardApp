using System.Windows;
namespace TickerBoard;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(new JsonSettingsService(), new YahooMarketDataProvider());
        MainWindow = window;
        window.Show();
    }
}
