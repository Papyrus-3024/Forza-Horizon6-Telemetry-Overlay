using System.Text.Json;
using System.Text.Json.Serialization;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Widgets;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class NamedLayoutTests
{
    // ─── SaveLayoutAs deep-copy isolation ───────────────────────────────────

    [Fact]
    public void SaveLayoutAs_snapshot_is_isolated_from_subsequent_live_changes()
    {
        var config = new OverlayConfig { Layout = OverlayLayout.BottomStrip, Scale = 1.0 };
        config.Widgets[WidgetId.Gear.ToString()] = new WidgetConfig { X = 10, Y = 20, Scale = 1.0 };

        config.SaveLayoutAs("snap");

        // Mutate the live config
        config.Widgets[WidgetId.Gear.ToString()].X = 999;
        config.Scale = 2.0;
        config.Layout = OverlayLayout.CenterDash;

        var saved = config.SavedLayouts["snap"];
        Assert.Equal(10, saved.Widgets[WidgetId.Gear.ToString()].X);
        Assert.Equal(1.0, saved.Scale);
        Assert.Equal(OverlayLayout.BottomStrip, saved.BaseLayout);
    }

    [Fact]
    public void SaveLayoutAs_overwrites_existing_snapshot_with_new_values()
    {
        var config = new OverlayConfig { Scale = 1.0 };
        config.Widgets[WidgetId.Speed.ToString()] = new WidgetConfig { X = 50 };
        config.SaveLayoutAs("snap");

        config.Widgets[WidgetId.Speed.ToString()].X = 200;
        config.Scale = 1.5;
        config.SaveLayoutAs("snap");

        var saved = config.SavedLayouts["snap"];
        Assert.Equal(200, saved.Widgets[WidgetId.Speed.ToString()].X);
        Assert.Equal(1.5, saved.Scale);
    }

    [Fact]
    public void SaveLayoutAs_preserves_Accent_and_Surface_in_snapshot()
    {
        var config = new OverlayConfig();
        config.Widgets[WidgetId.RpmShift.ToString()] = new WidgetConfig
        {
            Accent = "#FF5AD15A",
            Surface = "#C0101418",
        };
        config.SaveLayoutAs("coloured");

        var saved = config.SavedLayouts["coloured"];
        Assert.Equal("#FF5AD15A", saved.Widgets[WidgetId.RpmShift.ToString()].Accent);
        Assert.Equal("#C0101418", saved.Widgets[WidgetId.RpmShift.ToString()].Surface);
    }

    // ─── LoadLayout ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadLayout_replaces_live_widgets_scale_and_layout()
    {
        var config = new OverlayConfig { Layout = OverlayLayout.BottomStrip, Scale = 1.0 };
        config.Normalize(OverlayLayout.BottomStrip);
        config.Widgets[WidgetId.Gear.ToString()].X = 42;
        config.SaveLayoutAs("saved");

        // Alter the live config
        config.Widgets[WidgetId.Gear.ToString()].X = 999;
        config.Scale = 2.5;
        config.Layout = OverlayLayout.CenterDash;

        var result = config.LoadLayout("saved");

        Assert.True(result);
        Assert.Equal(1.0, config.Scale);
        Assert.Equal(OverlayLayout.BottomStrip, config.Layout);
        Assert.Equal(42, config.Widgets[WidgetId.Gear.ToString()].X);
    }

    [Fact]
    public void LoadLayout_returns_false_for_unknown_name()
    {
        var config = new OverlayConfig();
        var result = config.LoadLayout("does-not-exist");
        Assert.False(result);
    }

    [Fact]
    public void LoadLayout_live_mutations_after_load_do_not_alter_snapshot()
    {
        var config = new OverlayConfig();
        config.Widgets[WidgetId.Gear.ToString()] = new WidgetConfig { X = 10 };
        config.SaveLayoutAs("snap");

        config.LoadLayout("snap");

        // Mutate live widget after load
        config.Widgets[WidgetId.Gear.ToString()].X = 777;

        // Snapshot must be unchanged
        Assert.Equal(10, config.SavedLayouts["snap"].Widgets[WidgetId.Gear.ToString()].X);
    }

    [Fact]
    public void LoadLayout_calls_Normalize_filling_missing_widget_keys()
    {
        // Save a snapshot with an empty Widgets dict
        var config = new OverlayConfig { Layout = OverlayLayout.BottomStrip };
        config.SaveLayoutAs("empty");

        // Now load it — Normalize should fill all widget keys
        config.LoadLayout("empty");

        foreach (WidgetId id in Enum.GetValues<WidgetId>())
            Assert.True(config.Widgets.ContainsKey(id.ToString()), $"Missing: {id}");
    }

    // ─── DeleteLayout ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteLayout_removes_existing_name_returns_true()
    {
        var config = new OverlayConfig();
        config.SaveLayoutAs("to-delete");
        Assert.True(config.DeleteLayout("to-delete"));
        Assert.False(config.SavedLayouts.ContainsKey("to-delete"));
    }

    [Fact]
    public void DeleteLayout_missing_name_returns_false()
    {
        var config = new OverlayConfig();
        Assert.False(config.DeleteLayout("ghost"));
    }

    // ─── RenameLayout ────────────────────────────────────────────────────────

    [Fact]
    public void RenameLayout_moves_snapshot_to_new_name()
    {
        var config = new OverlayConfig { Scale = 1.5 };
        config.SaveLayoutAs("old");
        Assert.True(config.RenameLayout("old", "new"));

        Assert.False(config.SavedLayouts.ContainsKey("old"));
        Assert.True(config.SavedLayouts.ContainsKey("new"));
        Assert.Equal(1.5, config.SavedLayouts["new"].Scale);
    }

    [Fact]
    public void RenameLayout_returns_false_for_unknown_from()
    {
        var config = new OverlayConfig();
        Assert.False(config.RenameLayout("ghost", "target"));
    }

    [Fact]
    public void RenameLayout_returns_false_when_from_equals_to()
    {
        var config = new OverlayConfig();
        config.SaveLayoutAs("same");
        Assert.False(config.RenameLayout("same", "same"));
        // snapshot still exists
        Assert.True(config.SavedLayouts.ContainsKey("same"));
    }

    // ─── JSON round-trip via ConfigStore ─────────────────────────────────────

    [Fact]
    public void ConfigStore_round_trips_SavedLayouts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6-named-{Guid.NewGuid():N}.json");
        try
        {
            var config = new OverlayConfig
            {
                Layout = OverlayLayout.CornerPanel,
                Scale = 1.2,
            };
            config.Widgets[WidgetId.Speed.ToString()] = new WidgetConfig { X = 100, Y = 200, Scale = 1.1 };
            config.SaveLayoutAs("my layout");
            config.SaveLayoutAs("another");

            ConfigStore.Save(path, config);
            var loaded = ConfigStore.Load(path);

            Assert.Equal(2, loaded.SavedLayouts.Count);
            Assert.True(loaded.SavedLayouts.ContainsKey("my layout"));
            Assert.True(loaded.SavedLayouts.ContainsKey("another"));

            var snap = loaded.SavedLayouts["my layout"];
            Assert.Equal(OverlayLayout.CornerPanel, snap.BaseLayout);
            Assert.Equal(1.2, snap.Scale);
            Assert.Equal(100, snap.Widgets[WidgetId.Speed.ToString()].X);
            Assert.Equal(200, snap.Widgets[WidgetId.Speed.ToString()].Y);
            Assert.Equal(1.1, snap.Widgets[WidgetId.Speed.ToString()].Scale);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ConfigStore_empty_SavedLayouts_round_trips_as_empty_dict()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6-named-empty-{Guid.NewGuid():N}.json");
        try
        {
            var config = new OverlayConfig();
            ConfigStore.Save(path, config);
            var loaded = ConfigStore.Load(path);
            Assert.Empty(loaded.SavedLayouts);
        }
        finally { File.Delete(path); }
    }
}
