using Fh6.Telemetry.Overlay.Layouts;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

public enum OverlayLayout
{
    BottomStrip,
    CornerPanel,
    CenterDash,
}

/// <summary>
/// A named snapshot of a complete layout configuration (layout mode, scale, widget positions).
/// </summary>
public sealed class SavedLayout
{
    public OverlayLayout BaseLayout { get; set; }
    public double Scale { get; set; } = 1.0;
    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new();
}

public sealed class OverlayConfig
{
    public int Port { get; set; } = 20440;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public OverlayLayout Layout { get; set; } = OverlayLayout.BottomStrip;
    public double Opacity { get; set; } = 0.9;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Per-widget customization, keyed by <see cref="WidgetId.ToString()"/>.
    /// Absent keys mean "not yet customized"; <see cref="Normalize"/> fills them from the seed.
    /// </summary>
    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new();

    /// <summary>
    /// User-named layout snapshots, keyed by name.
    /// </summary>
    public Dictionary<string, SavedLayout> SavedLayouts { get; set; } = new();

    /// <summary>
    /// For each of the six <see cref="WidgetId"/> keys missing from <see cref="Widgets"/>,
    /// seeds from <see cref="LayoutSeeds.For(OverlayLayout)"/>.
    /// Keys that are already present are kept unchanged, except Scale is clamped to [0.5, 2.5].
    /// </summary>
    public void Normalize(OverlayLayout layout)
    {
        var seeds = LayoutSeeds.For(layout);

        // Clamp scale on any pre-existing entries
        foreach (var cfg in Widgets.Values)
            cfg.Scale = Math.Clamp(cfg.Scale, 0.5, 2.5);

        // Seed missing widget keys
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var key = id.ToString();
            if (!Widgets.ContainsKey(key))
            {
                var s = seeds[id];
                Widgets[key] = new WidgetConfig
                {
                    Visible = s.Visible,
                    X = s.X,
                    Y = s.Y,
                    Scale = s.Scale,
                };
            }
        }
    }

    /// <summary>
    /// Overwrites X/Y/Scale/Visible for all six widgets from the given seed dictionary.
    /// Does NOT touch Accent or Surface — color overrides survive a preset re-apply.
    /// </summary>
    public void ApplySeed(IReadOnlyDictionary<WidgetId, WidgetSeed> seed)
    {
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var key = id.ToString();
            var s = seed[id];

            if (!Widgets.TryGetValue(key, out var cfg))
            {
                cfg = new WidgetConfig();
                Widgets[key] = cfg;
            }

            cfg.X = s.X;
            cfg.Y = s.Y;
            cfg.Scale = s.Scale;
            cfg.Visible = s.Visible;
            // Accent and Surface are intentionally NOT touched.
        }
    }

    // ─── Named layout management ────────────────────────────────────────────

    private static WidgetConfig DeepCopyWidget(WidgetConfig src) => new()
    {
        Visible = src.Visible,
        X = src.X,
        Y = src.Y,
        Scale = src.Scale,
        Accent = src.Accent,
        Surface = src.Surface,
    };

    private static Dictionary<string, WidgetConfig> DeepCopyWidgets(
        Dictionary<string, WidgetConfig> source)
    {
        var copy = new Dictionary<string, WidgetConfig>(source.Count);
        foreach (var (k, v) in source)
            copy[k] = DeepCopyWidget(v);
        return copy;
    }

    /// <summary>
    /// Saves the current Layout, Scale, and Widgets as a named snapshot.
    /// Overwrites any existing snapshot with the same name.
    /// </summary>
    public void SaveLayoutAs(string name)
    {
        SavedLayouts[name] = new SavedLayout
        {
            BaseLayout = Layout,
            Scale = Scale,
            Widgets = DeepCopyWidgets(Widgets),
        };
    }

    /// <summary>
    /// Restores the named snapshot into the live config.
    /// Calls <see cref="Normalize"/> after loading so missing widget keys are filled.
    /// Returns false (unchanged) if <paramref name="name"/> does not exist.
    /// </summary>
    public bool LoadLayout(string name)
    {
        if (!SavedLayouts.TryGetValue(name, out var saved))
            return false;

        Layout = saved.BaseLayout;
        Scale = saved.Scale;
        Widgets = DeepCopyWidgets(saved.Widgets);
        Normalize(Layout);
        return true;
    }

    /// <summary>
    /// Removes the named snapshot. Returns false if the name did not exist.
    /// </summary>
    public bool DeleteLayout(string name) => SavedLayouts.Remove(name);

    /// <summary>
    /// Renames a snapshot from <paramref name="from"/> to <paramref name="to"/>.
    /// Returns false if <paramref name="from"/> does not exist or <paramref name="to"/> equals <paramref name="from"/>.
    /// Overwrites any existing snapshot at <paramref name="to"/>.
    /// </summary>
    public bool RenameLayout(string from, string to)
    {
        if (from == to) return false;
        if (!SavedLayouts.TryGetValue(from, out var saved)) return false;
        SavedLayouts.Remove(from);
        SavedLayouts[to] = saved;
        return true;
    }
}
