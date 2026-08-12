using LightningChart.LA.Api;
using LightningChart.LA.WebView;

namespace LightningChartMauiExample;

public sealed class MainPage : ContentPage, IAsyncDisposable
{
    private readonly WebView _webView = new();
    private readonly Label _status = new()
    {
        BackgroundColor = Colors.White,
        TextColor = Colors.Black,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        Padding = 24,
        Text = "Loading LightningChart…",
    };
    private WebViewTransport? _transport;
    private LclaContext? _context;
    private LclaChart? _chart;
    private bool _created;

    public MainPage()
    {
        Title = "LightningChart MAUI";
        _webView.Navigated += OnNavigated;
        var layout = new Grid();
        layout.Add(_webView);
        layout.Add(_status);
        Content = layout;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _transport = await WebViewTransport.StartAsync();
            _webView.Source = _transport.Uri.AbsoluteUri;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs args)
    {
        if (_created || _transport is null) return;
        _created = true;
        try
        {
            var licenseKey = Environment.GetEnvironmentVariable("LCJS_LICENSE_KEY")
                ?? throw new InvalidOperationException("Set LCJS_LICENSE_KEY before starting the example.");
            _context = new LclaContext(_transport, new LclaLicense { Key = licenseKey, AppTitle = "LightningChart MAUI Example" });
            _chart = await _context.CreateChartAsync(new XYChartConfig
            {
                ContainerId = "lcla-root",
                Title = "MAUI signal monitor",
                AnimationsEnabled = false,
                DataSets = [new DataSetConfig { Id = "signal", MaxSampleCount = 2_000_000, Columns = [new DataSetColumnConfig { Id = "value" }] }],
                Channels = [new ChannelConfig { Id = "value", DataSetId = "signal", Column = "value", Name = "Signal" }],
            });

            const int count = 1_000_000;
            var x = new double[count];
            var values = new double[count];
            for (var i = 0; i < count; i++)
            {
                x[i] = i * 0.001;
                values[i] = Math.Sin(x[i] * 8);
            }
            _chart.SetData(new SetDataOptions { DataSetId = "signal", X = x, Columns = new Dictionary<string, double[]> { ["value"] = values } });
            _chart.SetScrollStrategy(new SetScrollStrategyOptions { AxisX = ScrollStrategy.Scrolling });
            _chart.SetDefaultAxisInterval(new SetDefaultAxisIntervalOptions { Axis = AxisTarget.X, Length = 10 });
            _status.IsVisible = false;
            _ = StreamAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ShowError(Exception exception)
    {
        _status.Text = $"LightningChart could not start.\n\n{exception.Message}";
        _status.IsVisible = true;
    }

    private async Task StreamAsync()
    {
        if (_chart is null) return;
        var next = 1_000d;
        while (_chart is not null)
        {
            const int count = 10_000;
            var x = new double[count];
            var values = new double[count];
            for (var i = 0; i < count; i++)
            {
                x[i] = next;
                values[i] = Math.Sin(next * 8);
                next += 0.001;
            }
            _chart.AppendData(new AppendDataOptions { DataSetId = "signal", X = x, Columns = new Dictionary<string, double[]> { ["value"] = values } });
            await Task.Delay(16);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_chart is not null) await _chart.DisposeAsync();
        if (_context is not null) await _context.DisposeAsync();
        if (_transport is not null) await _transport.DisposeAsync();
    }
}
