using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Fh6.Telemetry.Overlay.Helpers;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.ViewModels;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Layouts;

public partial class FreeLayout : UserControl
{
    // Maps each WidgetId to its FrameworkElement (the widget UserControl itself).
    private readonly Dictionary<WidgetId, FrameworkElement> _widgets;

    // Edit-mode state
    private bool _editMode;

    // Drag state
    private WidgetId? _dragging;
    private Point _dragOffset; // cursor pos in Canvas coords minus widget Canvas.Left/Top

    public IReadOnlyDictionary<WidgetId, FrameworkElement> Widgets => _widgets;

    public FreeLayout()
    {
        InitializeComponent();

        // Instantiate the six widgets once.
        _widgets = new Dictionary<WidgetId, FrameworkElement>
        {
            [WidgetId.Gear]        = new GearWidget(),
            [WidgetId.Speed]       = new SpeedWidget(),
            [WidgetId.RpmShift]    = new RpmShiftWidget(),
            [WidgetId.PedalsSteer] = new PedalsSteerWidget(),
            [WidgetId.Boost]       = new BoostWidget(),
            [WidgetId.LapTiming]   = new LapTimingWidget(),
        };

        foreach (var w in _widgets.Values)
            Surface.Children.Add(w);

        // Drag handlers on the Canvas
        Surface.PreviewMouseLeftButtonDown += Surface_PreviewMouseLeftButtonDown;
        Surface.MouseMove += Surface_MouseMove;
        Surface.MouseLeftButtonUp += Surface_MouseLeftButtonUp;
    }

    /// <summary>
    /// Sets the shared TelemetryViewModel as DataContext so widgets inherit it.
    /// </summary>
    public void SetViewModel(TelemetryViewModel viewModel)
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// Applies position, scale, and visibility from the config to every widget,
    /// and applies the global HUD scale to the Canvas itself.
    /// </summary>
    public void ApplyConfig(OverlayConfig cfg)
    {
        var seeds = LayoutSeeds.For(cfg.Layout);
        var globalScale = Math.Clamp(cfg.Scale, 0.5, 3.0);

        // Global HUD scale on the Canvas itself.
        Surface.LayoutTransform = new ScaleTransform(globalScale, globalScale);

        foreach (var (id, widget) in _widgets)
        {
            var key = id.ToString();
            WidgetConfig? wc = cfg.Widgets.TryGetValue(key, out var found) ? found : null;

            // Position: use config value, fall back to seed.
            double x, y;
            if (wc?.X is double wx && wc?.Y is double wy)
            {
                x = wx;
                y = wy;
            }
            else
            {
                var seed = seeds.TryGetValue(id, out var s) ? s : default;
                x = seed.X;
                y = seed.Y;
            }

            Canvas.SetLeft(widget, x);
            Canvas.SetTop(widget, y);

            // Per-widget scale.
            var scale = wc is not null ? Math.Clamp(wc.Scale, 0.5, 2.5) : 1.0;
            widget.LayoutTransform = new ScaleTransform(scale, scale);

            // Visibility: in edit mode, ghosted instead of Collapsed.
            var visible = wc?.Visible ?? true;
            if (_editMode)
            {
                widget.Visibility = Visibility.Visible;
                widget.Opacity = visible ? 1.0 : 0.3;
            }
            else
            {
                widget.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                widget.Opacity = 1.0;
            }
        }
    }

    /// <summary>
    /// Enables or disables edit mode. In edit mode widgets show a yellow outline and
    /// hidden widgets are rendered ghosted (Opacity 0.3) instead of Collapsed.
    /// </summary>
    public void SetEditMode(bool editMode, OverlayConfig cfg)
    {
        _editMode = editMode;

        foreach (var (id, widget) in _widgets)
        {
            var key = id.ToString();
            var visible = cfg.Widgets.TryGetValue(key, out var wc) ? wc.Visible : true;

            if (editMode)
            {
                // Show all widgets; ghost hidden ones.
                widget.Visibility = Visibility.Visible;
                widget.Opacity = visible ? 1.0 : 0.3;

                // Yellow outline via Tag so we can detect it was set here.
                widget.Tag = "edit";
                widget.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Yellow,
                    ShadowDepth = 0,
                    BlurRadius = 6,
                    Opacity = 0.9,
                };
            }
            else
            {
                // Restore normal visibility.
                widget.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                widget.Opacity = 1.0;
                widget.Tag = null;
                widget.Effect = null;
            }
        }
    }

    // ----- Drag handling -----

    private void Surface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editMode) return;

        // Walk up from OriginalSource to find the owning Canvas child widget.
        var hitWidget = FindOwningWidget(e.OriginalSource as DependencyObject);
        if (hitWidget is null)
        {
            // Nothing hit — let the event fall through so the window-level DragMove fires.
            return;
        }

        // Identify which WidgetId was hit.
        _dragging = null;
        foreach (var (id, w) in _widgets)
        {
            if (ReferenceEquals(w, hitWidget))
            {
                _dragging = id;
                break;
            }
        }

        if (_dragging is null) return;

        // Record the offset: cursor in Canvas coords minus current widget position.
        var cursorOnCanvas = e.GetPosition(Surface);
        var left = Canvas.GetLeft(hitWidget);
        var top  = Canvas.GetTop(hitWidget);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top))  top  = 0;
        _dragOffset = new Point(cursorOnCanvas.X - left, cursorOnCanvas.Y - top);

        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null || !Surface.IsMouseCaptured) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ReleaseDrag();
            return;
        }

        var id = _dragging.Value;
        var widget = _widgets[id];

        var cursorOnCanvas = e.GetPosition(Surface);
        var desired = new Point(
            cursorOnCanvas.X - _dragOffset.X,
            cursorOnCanvas.Y - _dragOffset.Y);

        // Effective widget size after per-widget scale.
        var scale = widget.LayoutTransform is ScaleTransform st ? st.ScaleX : 1.0;
        var widgetSize = new Size(
            widget.ActualWidth  * scale,
            widget.ActualHeight * scale);
        var canvasSize = new Size(Surface.ActualWidth, Surface.ActualHeight);

        var clamped = DragMath.Clamp(desired, widgetSize, canvasSize);
        Canvas.SetLeft(widget, clamped.X);
        Canvas.SetTop(widget, clamped.Y);
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging is null) return;

        var id = _dragging.Value;
        var widget = _widgets[id];

        // The write-back to cfg.Widgets happens in the window when edit mode ends.
        // Here we just release capture.
        ReleaseDrag();
        e.Handled = true;
    }

    private void ReleaseDrag()
    {
        _dragging = null;
        if (Surface.IsMouseCaptured)
            Surface.ReleaseMouseCapture();
    }

    /// <summary>
    /// Writes the current Canvas positions of all widgets back into cfg.Widgets[id].X/Y.
    /// Called by OverlayWindow when leaving edit mode.
    /// </summary>
    public void FlushPositions(OverlayConfig cfg)
    {
        foreach (var (id, widget) in _widgets)
        {
            var key = id.ToString();
            if (!cfg.Widgets.TryGetValue(key, out var wc))
            {
                wc = new WidgetConfig();
                cfg.Widgets[key] = wc;
            }
            wc.X = Canvas.GetLeft(widget);
            wc.Y = Canvas.GetTop(widget);
        }
    }

    // ---- Visual tree helper ----

    /// <summary>
    /// Walks up from <paramref name="source"/> through the visual tree to find
    /// the first ancestor (or self) that is a direct child of <see cref="Surface"/>.
    /// Returns null if no Canvas child is found.
    /// </summary>
    private FrameworkElement? FindOwningWidget(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement fe && Surface.Children.Contains(fe))
                return fe;
            current = GetParentObject(current);
        }
        return null;
    }

    /// <summary>
    /// Returns the parent of a node, handling both Visuals and content elements (e.g. a
    /// <see cref="System.Windows.Documents.Run"/> inside a TextBlock). VisualTreeHelper.GetParent
    /// throws on non-Visuals, which is what caused the drag crash when clicking lap text.
    /// </summary>
    private static DependencyObject? GetParentObject(DependencyObject obj)
    {
        if (obj is Visual or System.Windows.Media.Media3D.Visual3D)
            return VisualTreeHelper.GetParent(obj);

        if (obj is ContentElement contentElement)
        {
            var parent = ContentOperations.GetParent(contentElement);
            if (parent is not null) return parent;
            return contentElement is FrameworkContentElement fce ? fce.Parent : null;
        }

        return LogicalTreeHelper.GetParent(obj);
    }
}
