namespace LightningChartMauiExample.WinUI;

public sealed partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => global::LightningChartMauiExample.MauiProgram.CreateMauiApp();
}
