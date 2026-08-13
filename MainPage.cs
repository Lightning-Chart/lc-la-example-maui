using LightningChart.LA.Api;
using LightningChart.LA.WebView;

namespace LightningChartMauiExample;

public sealed class MainPage : ContentPage, IAsyncDisposable
{
    private const int HistoricalPointCount = 1_000_000;
    private const int StreamBatchSize = 10_000;
    private readonly WebView _webView = new();
    private readonly Button _loadButton = new() { Text = "Load historical", IsEnabled = false };
    private readonly Button _streamButton = new() { Text = "Start streaming", IsEnabled = false };
    private readonly Label _mode = Metric("Mode", "Starting");
    private readonly Label _samples = Metric("Samples", "0");
    private readonly Label _historical = Metric("Historical", "Empty");
    private readonly Label _status = new() { Text = "Loading LightningChart…", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
    private readonly CancellationTokenSource _lifetime = new();
    private WebViewTransport? _transport;
    private LclaContext? _context;
    private LclaChart? _chart;
    private CancellationTokenSource? _streamCancellation;
    private bool _created;
    private bool _isStreaming;
    private int _sampleCount;
    private double _nextX;

    public MainPage()
    {
        Title = "LightningChart MAUI";
        BackgroundColor = Color.FromArgb("#080A0D");
        _webView.Navigated += OnNavigated;
        _loadButton.Clicked += async (_, _) => await LoadHistoricalDataAsync();
        _streamButton.Clicked += async (_, _) => await ToggleStreamingAsync();

        var header = new Grid { BackgroundColor = Color.FromArgb("#10151B"), Padding = new Thickness(16, 10), ColumnDefinitions = [new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto }] };
        header.Add(new Label { Text = "LightningChart MAUI", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center }, 0);
        header.Add(_loadButton, 1);
        header.Add(_streamButton, 2);

        var metrics = new HorizontalStackLayout { BackgroundColor = Color.FromArgb("#10151B"), Padding = new Thickness(16, 8), Spacing = 36, Children = { _mode, _samples, _historical } };
        var footer = new VerticalStackLayout { BackgroundColor = Color.FromArgb("#10151B"), Padding = new Thickness(16, 10), Children = { _status } };
        var layout = new Grid { RowDefinitions = [new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star }, new RowDefinition { Height = GridLength.Auto }] };
        layout.Add(header, 0);
        layout.Add(metrics, 0, 1);
        layout.Add(_webView, 0, 2);

        layout.Add(footer, 0, 3);
        Content = layout;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private static Label Metric(string label, string value) => new() { Text = $"{label}\n{value}", TextColor = Colors.White, FontSize = 14 };

    private async Task InitializeAsync()
    {
        try
        {
            _transport = await WebViewTransport.StartAsync(_lifetime.Token);
            _status.Text = "Waiting for the chart page…";
            _webView.Source = _transport.Uri.AbsoluteUri;
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs args)
    {
        if (_created || _transport is null || !Uri.TryCreate(args.Url, UriKind.Absolute, out var uri) || Uri.Compare(uri, _transport.Uri, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0) return;
        _created = true;
        try
        {
            _status.Text = "Loading chart…";
            var licenseKey = Environment.GetEnvironmentVariable("LCJS_LICENSE_KEY") ?? throw new InvalidOperationException("Set LCJS_LICENSE_KEY before starting the example.");
            _context = new LclaContext(_transport, new LclaLicense { Key = licenseKey });
            _context.ErrorOccurred += (_, eventArgs) => MainThread.BeginInvokeOnMainThread(() => ShowError(eventArgs.Exception));
            _chart = await _context.CreateChartAsync(new XYChartConfig
            {
                ContainerId = "lcla-root", Title = "High-Rate Signal Monitor", AnimationsEnabled = false,
                DataSets = [new DataSetConfig { Id = "signals", MaxSampleCount = 2_000_000, Columns = [new DataSetColumnConfig { Id = "raw" }, new DataSetColumnConfig { Id = "filtered" }] }],
                Channels = [new ChannelConfig { Id = "raw", DataSetId = "signals", Column = "raw", Name = "Raw signal", Color = "#9E9E9E" }, new ChannelConfig { Id = "filtered", DataSetId = "signals", Column = "filtered", Name = "Filtered", Color = "#00A6FF" }],
            });
            _loadButton.IsEnabled = true;
            _streamButton.IsEnabled = true;
            await LoadHistoricalDataAsync();
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async Task LoadHistoricalDataAsync()
    {
        if (_chart is null) return;
        await StopStreamingAsync();
        _loadButton.IsEnabled = false;
        try
        {
            var data = await Task.Run(CreateHistoricalData, _lifetime.Token);
            _chart.SetScrollStrategy(new SetScrollStrategyOptions { AxisX = ScrollStrategy.Fitting });
            _chart.SetData(new SetDataOptions { DataSetId = "signals", X = data.X, Columns = data.Columns });
            _chart.SetAxisInterval(new SetAxisIntervalOptions { Axis = AxisTarget.X, Start = 980, End = 1_000 });
            _sampleCount = HistoricalPointCount;
            _nextX = data.X[^1];
            UpdateMetrics("Historical", "Loaded");
        }
        catch (Exception exception) { ShowError(exception); }
        finally { _loadButton.IsEnabled = _chart is not null; }
    }

    private async Task ToggleStreamingAsync()
    {
        if (_isStreaming) await StopStreamingAsync();
        else StartStreaming();
    }

    private void StartStreaming()
    {
        if (_chart is null || _isStreaming) return;
        _chart.SetScrollStrategy(new SetScrollStrategyOptions { AxisX = ScrollStrategy.Scrolling });
        _chart.SetDefaultAxisInterval(new SetDefaultAxisIntervalOptions { Axis = AxisTarget.X, Length = 5 });
        _isStreaming = true;
        _streamButton.Text = "Stop streaming";
        UpdateMetrics("Live", "Loaded");
        _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _ = StreamAsync(_streamCancellation.Token);
    }

    private async Task StopStreamingAsync()
    {
        if (!_isStreaming) return;
        _isStreaming = false;
        _streamCancellation?.Cancel();
        _streamCancellation?.Dispose();
        _streamCancellation = null;
        _streamButton.Text = "Start streaming";
        UpdateMetrics("Historical", "Loaded");
        await Task.CompletedTask;
    }

    private async Task StreamAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _chart is not null)
            {
                var data = CreateStreamBatch();
                _chart.AppendData(new AppendDataOptions { DataSetId = "signals", X = data.X, Columns = data.Columns });
                _sampleCount += StreamBatchSize;
                if (_sampleCount % 100_000 == 0) MainThread.BeginInvokeOnMainThread(() => UpdateMetrics("Live", "Loaded"));
                await Task.Delay(16, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception) { MainThread.BeginInvokeOnMainThread(() => ShowError(exception)); }
    }

    private (double[] X, Dictionary<string, double[]> Columns) CreateHistoricalData()
    {
        var x = new double[HistoricalPointCount]; var raw = new double[HistoricalPointCount]; var filtered = new double[HistoricalPointCount]; var random = new Random(42);
        for (var i = 0; i < HistoricalPointCount; i++) { var time = i * 0.001; x[i] = time; filtered[i] = Math.Sin(time * 10); raw[i] = filtered[i] + Math.Sin(time * 77) * 0.35 + random.NextDouble() * 0.35 - 0.175; }
        return (x, new Dictionary<string, double[]> { ["raw"] = raw, ["filtered"] = filtered });
    }

    private (double[] X, Dictionary<string, double[]> Columns) CreateStreamBatch()
    {
        var x = new double[StreamBatchSize]; var raw = new double[StreamBatchSize]; var filtered = new double[StreamBatchSize];
        for (var i = 0; i < StreamBatchSize; i++) { var time = _nextX; x[i] = time; filtered[i] = Math.Sin(time * 10); raw[i] = filtered[i] + Math.Sin(time * 77) * 0.35; _nextX += 0.001; }
        return (x, new Dictionary<string, double[]> { ["raw"] = raw, ["filtered"] = filtered });
    }

    private void UpdateMetrics(string mode, string historical) { _mode.Text = $"Mode\n{mode}"; _samples.Text = $"Samples\n{FormatCount(_sampleCount)}"; _historical.Text = $"Historical\n{historical}"; _status.Text = mode == "Live" ? "Real-time updates active" : "Historical dataset ready"; _status.TextColor = Color.FromArgb("#8C98A4"); }
    private static string FormatCount(int count) => count >= 1_000_000 ? $"{count / 1_000_000d:0.0}M" : count >= 1_000 ? $"{count / 1_000d:0}k" : count.ToString();

    private void ShowError(Exception exception)
    {
        _status.Text = $"Chart error: {exception.Message}";
        _status.TextColor = Color.FromArgb("#FFB4AB");
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel(); await StopStreamingAsync();
        if (_chart is not null) await _chart.DisposeAsync();
        if (_context is not null) await _context.DisposeAsync();
        if (_transport is not null) await _transport.DisposeAsync();
        _lifetime.Dispose();
    }
}
