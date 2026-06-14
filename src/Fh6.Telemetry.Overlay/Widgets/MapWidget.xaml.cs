using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Settings;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class MapWidget : UserControl
{
    // ── Reference projection parameters (20 000 px map) ─────────────────────
    // Published FH6 world→pixel affine for a 20 000 px square reference map.
    // Scaled to the actual image width in Configure; nudged by MapScale/Offset.
    private const double RefA =  0.652837;
    private const double RefB =  0.000763;
    private const double RefC =  10387.027;
    private const double RefD = -0.003754;
    private const double RefE = -0.657135;
    private const double RefF =  9846.097;
    private const double RefMapSize = 20000.0;

    // ── DependencyProperties ─────────────────────────────────────────────────

    public static readonly DependencyProperty WorldXProperty =
        DependencyProperty.Register(
            nameof(WorldX), typeof(double), typeof(MapWidget),
            new PropertyMetadata(0.0, OnWorldCoordChanged));

    public static readonly DependencyProperty WorldZProperty =
        DependencyProperty.Register(
            nameof(WorldZ), typeof(double), typeof(MapWidget),
            new PropertyMetadata(0.0, OnWorldCoordChanged));

    public double WorldX
    {
        get => (double)GetValue(WorldXProperty);
        set => SetValue(WorldXProperty, value);
    }

    public double WorldZ
    {
        get => (double)GetValue(WorldZProperty);
        set => SetValue(WorldZProperty, value);
    }

    // ── Private state ────────────────────────────────────────────────────────

    private BitmapImage? _bitmap;
    private MapCalibration? _effectiveCal;   // affine scaled+nudged for this image
    private double _zoom = 4.0;              // display px per source px

    // ── Constructor ──────────────────────────────────────────────────────────

    public MapWidget() => InitializeComponent();

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads (or reloads) the map image and derives the effective transform from cfg.
    /// Safe to call with a null or missing path; degrades to a placeholder.
    /// </summary>
    public void Configure(string? imagePath, OverlayConfig cfg)
    {
        _zoom   = Math.Clamp(cfg.MapZoom, 1.0, 16.0);
        _bitmap = TryLoadBitmap(imagePath);

        if (_bitmap is not null)
        {
            MapImage.Source = _bitmap;
            // Derive effective calibration scaled to the loaded image's pixel width,
            // then apply user nudges. This assumes our seasonal images share the
            // reference projection; the nudge (scale/offset) lets the user align by eye
            // if the terrain under the marker drifts.
            _effectiveCal = BuildEffectiveCalibration(_bitmap.PixelWidth, cfg);
            HidePlaceholder();
        }
        else
        {
            MapImage.Source = null;
            _effectiveCal = null;
            ShowPlaceholder("No map image");
        }

        UpdateView();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private static void OnWorldCoordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MapWidget)d).UpdateView();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateView();

    // ── Car-centred view update ──────────────────────────────────────────────

    private void UpdateView()
    {
        if (_bitmap is null || _effectiveCal is null)
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        double viewW = Viewport.ActualWidth;
        double viewH = Viewport.ActualHeight;
        if (viewW <= 0 || viewH <= 0)
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        var (srcX, srcY) = WorldToMap.ToPixel(WorldX, WorldZ, _effectiveCal);
        if (double.IsNaN(srcX) || double.IsNaN(srcY) ||
            double.IsInfinity(srcX) || double.IsInfinity(srcY))
        {
            Marker.Visibility = Visibility.Collapsed;
            return;
        }

        // Scale the source image by zoom, then translate so the car pixel lands at viewport centre.
        // The marker Ellipse is centred in the Grid via HorizontalAlignment/VerticalAlignment=Center.
        MapScale.ScaleX = _zoom;
        MapScale.ScaleY = _zoom;
        MapTranslate.X  = viewW / 2.0 - srcX * _zoom;
        MapTranslate.Y  = viewH / 2.0 - srcY * _zoom;

        Marker.Visibility = Visibility.Visible;
    }

    // ── Default calibration construction ─────────────────────────────────────

    private static MapCalibration BuildEffectiveCalibration(int imagePixelWidth, OverlayConfig cfg)
    {
        // Scale reference params to match the loaded image's actual pixel width.
        double f = imagePixelWidth / RefMapSize;

        double a = RefA * f;
        double b = RefB * f;
        double c = RefC * f;
        double d = RefD * f;
        double e = RefE * f;
        double fParam = RefF * f;

        // Apply user nudges: MapScale scales the rotation/scale components (A,B,D,E);
        // MapOffsetX/Y shift the translation constants.
        double s = cfg.MapScale;
        return new MapCalibration
        {
            A = a * s,
            B = b * s,
            C = c + cfg.MapOffsetX,
            D = d * s,
            E = e * s,
            F = fParam + cfg.MapOffsetY,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BitmapImage? TryLoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            if (!File.Exists(path)) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource     = new Uri(path, UriKind.Absolute);
            bmp.CacheOption   = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            // Any I/O, format, or access error: degrade to placeholder.
            return null;
        }
    }

    private void ShowPlaceholder(string text)
    {
        Placeholder.Text = text;
        Placeholder.Visibility = Visibility.Visible;
    }

    private void HidePlaceholder()
    {
        Placeholder.Visibility = Visibility.Collapsed;
    }
}
