namespace MauiVitalSigns.WinUI;

public sealed partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => global::MauiVitalSigns.MauiProgram.CreateMauiApp();
}
