using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Settings;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class ChartWidget : UserControl
{
    // ── Configuration ────────────────────────────────────────────────────────

    private ChartHistory? _history;
    private double _windowSeconds = 60.0;

    // Which series are currently enabled (rebuilt by Configure).
    private readonly List<ChartSeriesDef> _enabledSeries = new();

    // ── Rendering resources ──────────────────────────────────────────────────

    // One line Path per series id — created once, shown/hidden, geometry swapped each frame.
    private readonly Dictionary<ChartSeriesId, Path> _paths = new();

    // One fill Path per series id — rendered behind the corresponding line path.
    private readonly Dictionary<ChartSeriesId, Path> _fillPaths = new();

    // One TextBlock per enabled series for the live value in the legend.
    // Rebuilt in Configure; keyed by series id.
    private readonly Dictionary<ChartSeriesId, TextBlock> _legendValues = new();

    // Reusable sample buffer — avoids per-frame allocation.
    private ChartSample[] _sampleBuf = Array.Empty<ChartSample>();

    // Decimation index buffer — sized to 2 × targetCols to accommodate min+max per bucket.
    private int[] _decBuf = Array.Empty<int>();

    // X-coordinate array reused per series (double[sampleCount]).
    private double[] _xBuf = Array.Empty<double>();

    // Per-series float value buffer used by DecimateMinMaxIndices.
    private float[] _valBuf = Array.Empty<float>();

    // ── Throttle state ───────────────────────────────────────────────────────

    private double _accumulatedDt;
    private const double RedrawIntervalSeconds = 1.0 / 28.0; // ~28 Hz

    // Latest timestamp seen at the last Redraw call; skip redraw if unchanged.
    private double _lastDrawnLatestTime = double.MinValue;

    // ── Constructor ──────────────────────────────────────────────────────────

    public ChartWidget()
    {
        InitializeComponent();

        // Pre-create one fill Path and one line Path per series.
        // Fill paths are added first (lower z-order), line paths after.
        foreach (var def in ChartSeriesCatalog.All)
        {
            // Fill path — translucent area under the line.
            var fillBrush = BuildFillBrush(def.Color);
            var fillPath = new Path
            {
                Fill             = fillBrush,
                Stroke           = null,
                Visibility       = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
            _fillPaths[def.Id] = fillPath;
            Plot.Children.Add(fillPath); // added first → behind line paths
        }

        foreach (var def in ChartSeriesCatalog.All)
        {
            // Line weight: Speed gets a slightly heavier stroke.
            double strokeWeight = def.Id == ChartSeriesId.Speed ? 1.8 : 1.4;

            var path = new Path
            {
                Stroke           = new SolidColorBrush(def.Color),
                StrokeThickness  = strokeWeight,
                Visibility       = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
            ((SolidColorBrush)path.Stroke).Freeze();
            _paths[def.Id] = path;
            Plot.Children.Add(path);
        }

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Gives the widget the ring-buffer it reads each tick.</summary>
    public void SetHistory(ChartHistory history)
    {
        _history = history;
        EnsureBuffers();
    }

    /// <summary>
    /// Applies chart configuration: time window and per-series visibility.
    /// Rebuilds the legend rows and shows/hides Paths accordingly.
    /// </summary>
    public void Configure(ChartConfig cfg)
    {
        _windowSeconds = cfg.WindowSeconds > 0 ? cfg.WindowSeconds : 60.0;

        _enabledSeries.Clear();
        foreach (var def in ChartSeriesCatalog.All)
        {
            bool on = ChartSeriesCatalog.IsEnabled(cfg, def.Id);
            var vis = on ? Visibility.Visible : Visibility.Collapsed;
            _paths[def.Id].Visibility     = vis;
            _fillPaths[def.Id].Visibility = vis;
            if (on) _enabledSeries.Add(def);
        }

        // Force a redraw after configuration change even if data hasn't advanced.
        _lastDrawnLatestTime = double.MinValue;

        RebuildLegend();
    }

    // ── Render tick ──────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
        => CompositionTarget.Rendering += OnRendering;

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => CompositionTarget.Rendering -= OnRendering;

    private void OnRendering(object? sender, EventArgs e)
    {
        // Accumulate dt using the render timing info when available.
        var args = e as System.Windows.Media.RenderingEventArgs;
        double dt = args is not null
            ? args.RenderingTime.TotalSeconds - _lastRenderTime
            : RedrawIntervalSeconds;
        _lastRenderTime = args?.RenderingTime.TotalSeconds ?? _lastRenderTime;

        // Guard against large jumps (e.g. first frame or window restore).
        if (dt < 0 || dt > 0.5) dt = RedrawIntervalSeconds;

        _accumulatedDt += dt;
        if (_accumulatedDt < RedrawIntervalSeconds) return;
        _accumulatedDt = 0;

        Redraw();
    }

    private double _lastRenderTime;

    // ── Redraw ───────────────────────────────────────────────────────────────

    private void Redraw()
    {
        double plotW = Plot.ActualWidth;
        double plotH = Plot.ActualHeight;

        if (_history is null || _history.Count == 0 || plotW <= 1 || plotH <= 1)
        {
            if (_lastDrawnLatestTime != double.MinValue)
            {
                _lastDrawnLatestTime = double.MinValue;
                ClearPaths();
            }
            return;
        }

        EnsureBuffers();

        // Copy the current window into _sampleBuf.
        int count = _history.CopyWindow(_windowSeconds, _sampleBuf);
        if (count == 0)
        {
            if (_lastDrawnLatestTime != double.MinValue)
            {
                _lastDrawnLatestTime = double.MinValue;
                ClearPaths();
            }
            return;
        }

        // Find the latest timestamp (newest sample is last in oldest-first order).
        double latestTime = _sampleBuf[count - 1].TimeSeconds;

        // Skip geometry rebuild if the data hasn't advanced since the last draw.
        if (latestTime == _lastDrawnLatestTime) return;
        _lastDrawnLatestTime = latestTime;

        // Build X array in pixel space (newest at right).
        EnsureXBuf(count);
        for (int i = 0; i < count; i++)
        {
            double tRel = latestTime - _sampleBuf[i].TimeSeconds; // seconds before "now"
            _xBuf[i] = plotW - tRel / _windowSeconds * plotW;
        }

        int targetCols = Math.Max(1, (int)plotW);
        // DecimateMinMaxIndices can emit up to 2 indices per bucket.
        EnsureDecBuf(targetCols * 2 + 2);
        EnsureValBuf(count);

        var xSpan = new ReadOnlySpan<double>(_xBuf, 0, count);

        // Build StreamGeometry for each enabled series using min/max decimation.
        foreach (var def in _enabledSeries)
        {
            var path     = _paths[def.Id];
            var fillPath = _fillPaths[def.Id];
            if (path.Visibility != Visibility.Visible) continue;

            // Extract float values for this series into _valBuf for DecimateMinMaxIndices.
            for (int i = 0; i < count; i++)
                _valBuf[i] = def.Select(_sampleBuf[i]);

            var valSpan  = new ReadOnlySpan<float>(_valBuf, 0, count);
            int decCount = ChartMath.DecimateMinMaxIndices(xSpan, valSpan, targetCols, _decBuf.AsSpan());

            // ── Line geometry ────────────────────────────────────────────────
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                BuildLineGeometry(ctx, decCount, def, plotH, isFilled: false);
            }
            sg.Freeze();
            path.Data = sg;

            // ── Fill geometry ────────────────────────────────────────────────
            // Reuse the same decimated indices to trace the line, then close to baseline.
            if (decCount > 0)
            {
                var fillSg = new StreamGeometry();
                using (var ctx = fillSg.Open())
                {
                    // Trace the line portion as a filled+closed figure.
                    BuildLineGeometry(ctx, decCount, def, plotH, isFilled: true);

                    // Close to baseline: go to bottom-right, then bottom-left.
                    int lastIdx  = _decBuf[decCount - 1];
                    int firstIdx = _decBuf[0];
                    ctx.LineTo(new Point(_xBuf[lastIdx],  plotH), isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(new Point(_xBuf[firstIdx], plotH), isStroked: false, isSmoothJoin: false);
                }
                fillSg.Freeze();
                fillPath.Data = fillSg;
            }
            else
            {
                fillPath.Data = null;
            }
        }

        UpdateLegend(count);
    }

    /// <summary>
    /// Emits the decimated line points into an open StreamGeometryContext.
    /// <paramref name="isFilled"/> controls the BeginFigure isFilled flag.
    /// </summary>
    private void BuildLineGeometry(
        StreamGeometryContext ctx,
        int decCount,
        ChartSeriesDef def,
        double plotH,
        bool isFilled)
    {
        bool first = true;
        int prevDecIdx = -1;

        for (int di = 0; di < decCount; di++)
        {
            int idx    = _decBuf[di];
            double x   = _xBuf[idx];
            float raw  = _valBuf[idx];
            double norm = ChartMath.Normalize(raw, def.Min, def.Max);
            double y    = (1.0 - norm) * plotH;

            if (first)
            {
                ctx.BeginFigure(new Point(x, y), isFilled: isFilled, isClosed: false);
                first = false;
            }
            else if (def.Stepped)
            {
                int prevIdx     = _decBuf[prevDecIdx];
                float prevRaw   = _valBuf[prevIdx];
                double prevNorm = ChartMath.Normalize(prevRaw, def.Min, def.Max);
                double prevY    = (1.0 - prevNorm) * plotH;

                ctx.LineTo(new Point(x, prevY), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(x, y),     isStroked: true, isSmoothJoin: false);
            }
            else
            {
                ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }

            prevDecIdx = di;
        }
    }

    private void ClearPaths()
    {
        foreach (var p in _paths.Values)
            p.Data = null;
        foreach (var p in _fillPaths.Values)
            p.Data = null;
        UpdateLegend(0);
    }

    // ── Area fill brush ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds a vertical LinearGradientBrush for the area fill:
    /// series color at ~35% alpha at the top, transparent at the bottom.
    /// The brush is frozen (allocation-free after creation).
    /// </summary>
    private static LinearGradientBrush BuildFillBrush(Color seriesColor)
    {
        var topColor    = Color.FromArgb(0x59, seriesColor.R, seriesColor.G, seriesColor.B); // ~35%
        var bottomColor = Color.FromArgb(0x00, seriesColor.R, seriesColor.G, seriesColor.B); // 0%

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
        brush.GradientStops.Add(new GradientStop(topColor,    0.0));
        brush.GradientStops.Add(new GradientStop(bottomColor, 1.0));
        brush.Freeze();
        return brush;
    }

    // ── Legend ───────────────────────────────────────────────────────────────

    private void RebuildLegend()
    {
        Legend.Children.Clear();
        _legendValues.Clear();

        foreach (var def in _enabledSeries)
        {
            // Row: [swatch] [name] / [value]
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 4) };

            var header = new StackPanel { Orientation = Orientation.Horizontal };

            var swatch = new Border
            {
                Width = 8,
                Height = 8,
                Background = new SolidColorBrush(def.Color),
                Margin = new Thickness(0, 1, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ((SolidColorBrush)swatch.Background).Freeze();

            var name = new TextBlock
            {
                Text = def.Name,
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 8,
                FontFamily = new FontFamily("Consolas"),
            };

            header.Children.Add(swatch);
            header.Children.Add(name);

            var value = new TextBlock
            {
                Text = "—",
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF2, 0xF4, 0xF7)),
                FontSize = 8,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(11, 0, 0, 0),
            };

            row.Children.Add(header);
            row.Children.Add(value);
            Legend.Children.Add(row);
            _legendValues[def.Id] = value;
        }
    }

    private void UpdateLegend(int sampleCount)
    {
        if (sampleCount == 0)
        {
            foreach (var tb in _legendValues.Values)
                tb.Text = "—";
            return;
        }

        // Use the latest sample (last in oldest-first order).
        ref readonly ChartSample latest = ref _sampleBuf[sampleCount - 1];

        foreach (var def in _enabledSeries)
        {
            if (!_legendValues.TryGetValue(def.Id, out var tb)) continue;
            float raw = def.Select(latest);
            tb.Text = def.FormatValue(raw);
        }
    }

    // ── Buffer helpers ───────────────────────────────────────────────────────

    private void EnsureBuffers()
    {
        int cap = _history?.Capacity ?? 8000;
        if (_sampleBuf.Length < cap)
            _sampleBuf = new ChartSample[cap];
    }

    private void EnsureXBuf(int count)
    {
        if (_xBuf.Length < count)
            _xBuf = new double[count];
    }

    private void EnsureDecBuf(int count)
    {
        if (_decBuf.Length < count)
            _decBuf = new int[count];
    }

    private void EnsureValBuf(int count)
    {
        if (_valBuf.Length < count)
            _valBuf = new float[count];
    }
}
