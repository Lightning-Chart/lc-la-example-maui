using System.Globalization;
using LightningChart.LA.Api;
using LightningChart.LA.WebView;

namespace LightningChartMauiExample;

public sealed class MainPage : ContentPage, IAsyncDisposable
{
    private const string DataFileName = "patient_10.csv";
    private const string DataSetId = "patient-vitals";
    private const double VisibleWindowSeconds = 20;
    private readonly WebView _webView = new();
    private readonly Button _loadButton = new() { Text = "Load historical", IsEnabled = false };
    private readonly Button _streamButton = new() { Text = "Start replay", IsEnabled = false };
    private readonly Label _heartRate = MetricValue();
    private readonly Label _bloodPressure = MetricValue();
    private readonly Label _spo2 = MetricValue();
    private readonly Label _respiratoryRate = MetricValue();
    private readonly Label _status = new()
    {
        Text = "Loading patient recording…",
        TextColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
    };
    private readonly CancellationTokenSource _lifetime = new();
    private WebViewTransport? _transport;
    private LclaContext? _context;
    private LclaChart? _chart;
    private double[]? _time;
    private Dictionary<string, double[]>? _columns;
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private bool _created;
    private bool _isStreaming;
    private int _streamIndex;

    public MainPage()
    {
        Title = "LightningChart MAUI";
        BackgroundColor = Color.FromArgb("#080A0D");
        _webView.Navigated += OnNavigated;
        _loadButton.Clicked += async (_, _) => await LoadHistoricalDataAsync();
        _streamButton.Clicked += async (_, _) => await ToggleStreamingAsync();

        var header = new Grid
        {
            BackgroundColor = Color.FromArgb("#10151B"),
            Padding = new Thickness(16, 10),
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            ],
        };
        header.Add(new Label
        {
            Text = "LightningChart MAUI",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalTextAlignment = TextAlignment.Center,
        }, 0);
        header.Add(_loadButton, 1);
        header.Add(_streamButton, 2);

        var metrics = new Grid
        {
            BackgroundColor = Color.FromArgb("#10151B"),
            Padding = new Thickness(12),
            RowSpacing = 12,
            WidthRequest = 240,
            RowDefinitions =
            [
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Star },
            ],
        };
        metrics.Add(MetricCard("Heart rate", _heartRate, "#F2C14E"), 0, 0);
        metrics.Add(MetricCard("Blood pressure", _bloodPressure, "#FF6B6B"), 0, 1);
        metrics.Add(MetricCard("SpO₂", _spo2, "#B388FF"), 0, 2);
        metrics.Add(MetricCard("Respiratory rate", _respiratoryRate, "#7BC96F"), 0, 3);
        var footer = new VerticalStackLayout
        {
            BackgroundColor = Color.FromArgb("#10151B"),
            Padding = new Thickness(16, 10),
            Children = { _status },
        };
        var layout = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            ],
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            ],
        };
        layout.Add(header, 0, 0);
        layout.Add(_webView, 0, 1);
        layout.Add(metrics, 1, 1);
        layout.Add(footer, 0, 2);
        Grid.SetColumnSpan(header, 2);
        Grid.SetColumnSpan(footer, 2);

        Content = layout;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private static Label MetricValue() => new()
    {
        Text = "—",
        TextColor = Colors.White,
        FontSize = 24,
        FontAttributes = FontAttributes.Bold,
        VerticalTextAlignment = TextAlignment.Center,
    };

    private static Border MetricCard(string title, Label value, string accentColor)
    {
        var content = new Grid { ColumnDefinitions = [new ColumnDefinition { Width = 5 }, new ColumnDefinition { Width = GridLength.Star }] };
        content.Add(new BoxView { Color = Color.FromArgb(accentColor), }, 0, 0);
        content.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16, 12),
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, TextColor = Color.FromArgb("#AAB4BE"), FontSize = 12, FontAttributes = FontAttributes.Bold },
                value,
            },
        }, 1, 0);

        return new Border
        {
            BackgroundColor = Color.FromArgb("#171D24"),
            Stroke = Color.FromArgb("#2B343E"),
            StrokeThickness = 1,
            Content = content,
        };
    }

    private async Task InitializeAsync()
    {
        if (_transport is not null) return;

        try
        {
            (_time, _columns) = await LoadPatientDataAsync(_lifetime.Token);
            _transport = await WebViewTransport.StartAsync(_lifetime.Token);
            _status.Text = "Waiting for the chart page…";
            _webView.Source = _transport.Uri.AbsoluteUri;
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs args)
    {
        if (_created || _transport is null || _time is null || _columns is null ||
            !Uri.TryCreate(args.Url, UriKind.Absolute, out var uri) ||
            Uri.Compare(uri, _transport.Uri,
                UriComponents.SchemeAndServer | UriComponents.Path,
                UriFormat.Unescaped,
                StringComparison.OrdinalIgnoreCase) != 0)
        {
            return;
        }

        _created = true;

        try
        {
            _status.Text = "Loading chart…";
            var licenseKey = Environment.GetEnvironmentVariable("LCJS_LICENSE_KEY")
                ?? throw new InvalidOperationException(
                    "Set LCJS_LICENSE_KEY before starting the example.");

            _context = new LclaContext(_transport, new LclaLicense { Key = licenseKey });
            _context.ErrorOccurred += (_, eventArgs) =>
                MainThread.BeginInvokeOnMainThread(() => ShowError(eventArgs.Exception));

            _chart = await _context.CreateChartAsync(new XYChartConfig
            {
                ContainerId = "lcla-root",
                Title = "Patient vital signs",
                DataSets =
                [
                    new DataSetConfig
                    {
                        Id = DataSetId,
                        XDataPattern = DataPattern.Progressive,
                        MaxSampleCount = _time.Length,
                        Columns =
                        [
                            new DataSetColumnConfig { Id = "heart-rate" },
                            new DataSetColumnConfig { Id = "systolic" },
                            new DataSetColumnConfig { Id = "diastolic" },
                            new DataSetColumnConfig { Id = "spo2" },
                            new DataSetColumnConfig { Id = "respiratory-rate" },
                        ],
                    },
                ],
                Channels =
                [
                    new ChannelConfig { Id = "heart-rate", DataSetId = DataSetId, Column = "heart-rate", Name = "Heart rate (bpm)", Color = "#F2C14E", StackIndex = 0 },
                    new ChannelConfig { Id = "systolic", DataSetId = DataSetId, Column = "systolic", Name = "Systolic (mmHg)", Color = "#FF6B6B", StackIndex = -1 },
                    new ChannelConfig { Id = "diastolic", DataSetId = DataSetId, Column = "diastolic", Name = "Diastolic (mmHg)", Color = "#F08A5D", StackIndex = -1 },
                    new ChannelConfig { Id = "spo2", DataSetId = DataSetId, Column = "spo2", Name = "SpO₂ (%)", Color = "#B388FF", StackIndex = -2 },
                    new ChannelConfig { Id = "respiratory-rate", DataSetId = DataSetId, Column = "respiratory-rate", Name = "Respiratory rate (/min)", Color = "#7BC96F", StackIndex = -3 },
                ],
            });

            _loadButton.IsEnabled = true;
            _streamButton.IsEnabled = true;
            await LoadHistoricalDataAsync();
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async Task LoadHistoricalDataAsync()
    {
        if (_chart is null || _time is null || _columns is null) return;
        await StopStreamingAsync();
        _loadButton.IsEnabled = false;

        try
        {
            _chart.SetScrollStrategy(new SetScrollStrategyOptions { AxisX = ScrollStrategy.Fitting });
            _chart.SetData(new SetDataOptions { DataSetId = DataSetId, X = _time, Columns = _columns });

            _chart.SetAxisInterval(new SetAxisIntervalOptions
            {
                Axis = AxisTarget.X,
                Start = Math.Max(_time[0], _time[^1] - VisibleWindowSeconds),
                End = _time[^1],
            });

            _streamIndex = _time.Length;
            UpdateMetrics(_time.Length - 1);
            _streamButton.Text = "Start replay";
            _status.Text = "Historical patient recording loaded.";
            _status.TextColor = Color.FromArgb("#8C98A4");
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
        if (_chart is null || _time is null || _columns is null || _isStreaming) return;

        if (_streamIndex >= _time.Length)
        {
            _chart.ClearData(new ClearDataOptions { DataSetId = DataSetId });
            _chart.SetScrollStrategy(new SetScrollStrategyOptions { AxisX = ScrollStrategy.Scrolling });
            _chart.SetDefaultAxisInterval(new SetDefaultAxisIntervalOptions
            {
                Axis = AxisTarget.X,
                Length = VisibleWindowSeconds,
            });
            _streamIndex = 0;
        }

        _streamCancellation?.Dispose();
        _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _isStreaming = true;
        _streamButton.Text = "Pause replay";
        _status.Text = "Patient recording replay is running.";
        _status.TextColor = Color.FromArgb("#8C98A4");
        _streamTask = StreamAsync(_streamCancellation.Token);
    }

    private async Task StopStreamingAsync()
    {
        if (!_isStreaming) return;

        _isStreaming = false;
        _streamCancellation?.Cancel();
        if (_streamTask is not null) await _streamTask;

        _streamCancellation?.Dispose();
        _streamCancellation = null;
        _streamTask = null;
        _streamButton.Text = "Resume replay";

        if (_time is not null && _streamIndex > 0)
        {
            _status.Text =
                $"Patient recording replay paused at {_time[_streamIndex - 1]:0.0} seconds.";
        }
    }

    private async Task StreamAsync(CancellationToken cancellationToken)
    {
        if (_chart is null || _time is null || _columns is null) return;

        try
        {
            while (_streamIndex < _time.Length)
            {
                var index = _streamIndex++;

                _chart.AppendData(new AppendDataOptions
                {
                    DataSetId = DataSetId,
                    X = [_time[index]],
                    Columns = new Dictionary<string, double[]>
                    {
                        ["heart-rate"] = [_columns["heart-rate"][index]],
                        ["systolic"] = [_columns["systolic"][index]],
                        ["diastolic"] = [_columns["diastolic"][index]],
                        ["spo2"] = [_columns["spo2"][index]],
                        ["respiratory-rate"] = [_columns["respiratory-rate"][index]],
                    },
                });

                UpdateMetrics(index);

                if (_streamIndex < _time.Length)
                {
                    var delay = _time[_streamIndex] - _time[index];
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
            }

            _isStreaming = false;
            _streamButton.Text = "Replay again";
            _status.Text = "Patient recording replay complete.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when replay is paused or the page is closed
        }
        catch (Exception exception)
        {
            _isStreaming = false;
            _streamButton.Text = "Start replay";
            ShowError(exception);
        }
    }

    private void UpdateMetrics(int index)
    {
        if (_columns is null) return;

        _heartRate.Text = $"{_columns["heart-rate"][index]:0} bpm";
        _bloodPressure.Text = $"{_columns["systolic"][index]:0}/{_columns["diastolic"][index]:0} mmHg";
        _spo2.Text = $"{_columns["spo2"][index]:0.0} %";
        _respiratoryRate.Text = $"{_columns["respiratory-rate"][index]:0.0} /min";
    }

    private static async Task<(double[] Time, Dictionary<string, double[]> Columns)>
        LoadPatientDataAsync(CancellationToken cancellationToken)
    {
        await using var stream =
            await FileSystem.Current.OpenAppPackageFileAsync(DataFileName);
        using var reader = new StreamReader(stream);

        await reader.ReadLineAsync(cancellationToken); // Header

        var time = new List<double>();
        var heartRate = new List<double>();
        var systolic = new List<double>();
        var diastolic = new List<double>();
        var spo2 = new List<double>();
        var respiratoryRate = new List<double>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var values = line.Split(',');
            if (values.Length != 6) continue;

            time.Add(Parse(values[0]));
            heartRate.Add(Parse(values[1]));
            systolic.Add(Parse(values[2]));
            diastolic.Add(Parse(values[3]));
            spo2.Add(Parse(values[4]));
            respiratoryRate.Add(Parse(values[5]));
        }

        if (time.Count == 0) throw new InvalidDataException($"{DataFileName} is empty.");

        return
        (
            time.ToArray(),
            new Dictionary<string, double[]>
            {
                ["heart-rate"] = heartRate.ToArray(),
                ["systolic"] = systolic.ToArray(),
                ["diastolic"] = diastolic.ToArray(),
                ["spo2"] = spo2.ToArray(),
                ["respiratory-rate"] = respiratoryRate.ToArray(),
            }
        );
    }

    private static double Parse(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private void ShowError(Exception exception)
    {
        _status.Text = $"Chart error: {exception.Message}";
        _status.TextColor = Color.FromArgb("#FFB4AB");
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _streamCancellation?.Cancel();
        if (_streamTask is not null) await _streamTask;

        if (_chart is not null) await _chart.DisposeAsync();
        if (_context is not null) await _context.DisposeAsync();
        if (_transport is not null) await _transport.DisposeAsync();

        _streamCancellation?.Dispose();
        _lifetime.Dispose();
    }
}
