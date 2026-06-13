using System.Windows;
using Fh6.Telemetry.Overlay.Helpers;
using Fh6.Telemetry.Overlay.Layouts;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Tests;

public class CustomizationModelTests
{
    // ─── Normalize ──────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_empty_config_seeds_all_widgets_for_BottomStrip()
    {
        var config = new OverlayConfig { Layout = OverlayLayout.BottomStrip };
        config.Normalize(OverlayLayout.BottomStrip);

        var expectedCount = Enum.GetValues<WidgetId>().Length;
        Assert.Equal(expectedCount, config.Widgets.Count);
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
            Assert.True(config.Widgets.ContainsKey(id.ToString()), $"Missing key: {id}");
    }

    [Fact]
    public void Normalize_empty_config_seeds_all_widgets_for_CornerPanel()
    {
        var config = new OverlayConfig { Layout = OverlayLayout.CornerPanel };
        config.Normalize(OverlayLayout.CornerPanel);

        var expectedCount = Enum.GetValues<WidgetId>().Length;
        Assert.Equal(expectedCount, config.Widgets.Count);
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
            Assert.True(config.Widgets.ContainsKey(id.ToString()), $"Missing key: {id}");
    }

    [Fact]
    public void Normalize_empty_config_seeds_all_widgets_for_CenterDash()
    {
        var config = new OverlayConfig { Layout = OverlayLayout.CenterDash };
        config.Normalize(OverlayLayout.CenterDash);

        var expectedCount = Enum.GetValues<WidgetId>().Length;
        Assert.Equal(expectedCount, config.Widgets.Count);
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
            Assert.True(config.Widgets.ContainsKey(id.ToString()), $"Missing key: {id}");
    }

    [Fact]
    public void Normalize_partial_config_fills_missing_and_preserves_present()
    {
        const double customX = 999.0;
        var config = new OverlayConfig { Layout = OverlayLayout.BottomStrip };
        config.Widgets[WidgetId.Gear.ToString()] = new WidgetConfig { X = customX };

        config.Normalize(OverlayLayout.BottomStrip);

        // All widgets present
        Assert.Equal(Enum.GetValues<WidgetId>().Length, config.Widgets.Count);
        // Custom X preserved on the pre-existing key
        Assert.Equal(customX, config.Widgets[WidgetId.Gear.ToString()].X);
        // Other keys were seeded (not null X from the seed)
        Assert.NotNull(config.Widgets[WidgetId.Speed.ToString()].X);
    }

    [Fact]
    public void Normalize_clamps_scale_below_minimum()
    {
        var config = new OverlayConfig();
        config.Widgets[WidgetId.Speed.ToString()] = new WidgetConfig { Scale = 0.1 };
        config.Normalize(OverlayLayout.BottomStrip);

        Assert.Equal(0.5, config.Widgets[WidgetId.Speed.ToString()].Scale);
    }

    [Fact]
    public void Normalize_clamps_scale_above_maximum()
    {
        var config = new OverlayConfig();
        config.Widgets[WidgetId.Speed.ToString()] = new WidgetConfig { Scale = 9.9 };
        config.Normalize(OverlayLayout.BottomStrip);

        Assert.Equal(2.5, config.Widgets[WidgetId.Speed.ToString()].Scale);
    }

    // ─── ApplySeed ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplySeed_overwrites_position_scale_visible_for_all_widgets()
    {
        var config = new OverlayConfig();
        var seed = LayoutSeeds.For(OverlayLayout.BottomStrip);
        config.ApplySeed(seed);

        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            Assert.True(config.Widgets.ContainsKey(id.ToString()));
            var w = config.Widgets[id.ToString()];
            var s = seed[id];
            Assert.Equal(s.X, w.X);
            Assert.Equal(s.Y, w.Y);
            Assert.Equal(s.Scale, w.Scale);
            Assert.Equal(s.Visible, w.Visible);
        }
    }

    [Fact]
    public void ApplySeed_does_not_clear_existing_Accent_and_Surface()
    {
        const string existingAccent = "#FF5AD15A";
        const string existingSurface = "#C0101418";

        var config = new OverlayConfig();
        config.Widgets[WidgetId.RpmShift.ToString()] = new WidgetConfig
        {
            Accent = existingAccent,
            Surface = existingSurface,
        };

        config.ApplySeed(LayoutSeeds.For(OverlayLayout.BottomStrip));

        var w = config.Widgets[WidgetId.RpmShift.ToString()];
        Assert.Equal(existingAccent, w.Accent);
        Assert.Equal(existingSurface, w.Surface);
    }

    // ─── LayoutSeeds ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OverlayLayout.BottomStrip)]
    [InlineData(OverlayLayout.CornerPanel)]
    [InlineData(OverlayLayout.CenterDash)]
    public void LayoutSeeds_For_returns_all_widget_ids(OverlayLayout layout)
    {
        var seeds = LayoutSeeds.For(layout);
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
            Assert.True(seeds.ContainsKey(id), $"Missing WidgetId: {id} for layout {layout}");
    }

    [Fact]
    public void LayoutSeeds_BottomStrip_arranges_as_horizontal_row()
    {
        // All widgets should have similar Y and increasing X
        var seeds = LayoutSeeds.For(OverlayLayout.BottomStrip);
        var yValues = seeds.Values.Select(s => s.Y).Distinct().ToList();
        // They should all be close to the same Y
        Assert.True(yValues.Max() - yValues.Min() < 10, "BottomStrip should be a horizontal row");
    }

    // ─── ColorOverrides ─────────────────────────────────────────────────────

    [Fact]
    public void ColorOverrides_TryParse_valid_hex_returns_non_null_brush()
    {
        var brush = ColorOverrides.TryParse("#FF5AD15A");
        Assert.NotNull(brush);
    }

    [Fact]
    public void ColorOverrides_TryParse_invalid_string_returns_null()
    {
        var brush = ColorOverrides.TryParse("nope");
        Assert.Null(brush);
    }

    [Fact]
    public void ColorOverrides_TryParse_null_returns_null()
    {
        var brush = ColorOverrides.TryParse(null);
        Assert.Null(brush);
    }

    [Fact]
    public void ColorOverrides_ToHex_null_returns_null()
    {
        var hex = ColorOverrides.ToHex(null);
        Assert.Null(hex);
    }

    [Fact]
    public void ColorOverrides_TryParse_then_ToHex_round_trips()
    {
        const string original = "#FF5AD15A";
        var brush = ColorOverrides.TryParse(original);
        Assert.NotNull(brush);
        var hex = ColorOverrides.ToHex(brush);
        Assert.Equal(original, hex, ignoreCase: true);
    }

    // ─── DragMath ───────────────────────────────────────────────────────────

    [Fact]
    public void DragMath_Clamp_inside_bounds_returns_desired()
    {
        var desired = new Point(100, 50);
        var widget = new Size(80, 40);
        var canvas = new Size(400, 300);

        var result = DragMath.Clamp(desired, widget, canvas);

        Assert.Equal(100, result.X);
        Assert.Equal(50, result.Y);
    }

    [Fact]
    public void DragMath_Clamp_negative_desired_returns_zero()
    {
        var desired = new Point(-50, -20);
        var widget = new Size(80, 40);
        var canvas = new Size(400, 300);

        var result = DragMath.Clamp(desired, widget, canvas);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
    }

    [Fact]
    public void DragMath_Clamp_desired_beyond_right_bottom_clamps_to_max()
    {
        var desired = new Point(500, 400);
        var widget = new Size(80, 40);
        var canvas = new Size(400, 300);

        var result = DragMath.Clamp(desired, widget, canvas);

        Assert.Equal(320, result.X); // 400 - 80
        Assert.Equal(260, result.Y); // 300 - 40
    }

    [Fact]
    public void DragMath_Clamp_widget_larger_than_canvas_returns_zero()
    {
        var desired = new Point(100, 100);
        var widget = new Size(500, 400);
        var canvas = new Size(400, 300);

        var result = DragMath.Clamp(desired, widget, canvas);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
    }

    // ─── JSON round-trip via ConfigStore ────────────────────────────────────

    [Fact]
    public void ConfigStore_round_trips_OverlayConfig_with_populated_Widgets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6cfg-widgets-{Guid.NewGuid():N}.json");
        try
        {
            var config = new OverlayConfig
            {
                Port = 20440,
                Layout = OverlayLayout.BottomStrip,
            };
            config.Widgets[WidgetId.Gear.ToString()] = new WidgetConfig
            {
                Visible = true,
                X = 6,
                Y = 24,
                Scale = 1.2,
                Accent = "#FF5AD15A",
                Surface = "#C0101418",
            };
            config.Widgets[WidgetId.Boost.ToString()] = new WidgetConfig
            {
                Visible = false,
                X = 360,
                Y = 24,
                Scale = 1.0,
            };

            ConfigStore.Save(path, config);
            var loaded = ConfigStore.Load(path);

            Assert.Equal(2, loaded.Widgets.Count);

            var gear = loaded.Widgets[WidgetId.Gear.ToString()];
            Assert.True(gear.Visible);
            Assert.Equal(6, gear.X);
            Assert.Equal(24, gear.Y);
            Assert.Equal(1.2, gear.Scale);
            Assert.Equal("#FF5AD15A", gear.Accent);
            Assert.Equal("#C0101418", gear.Surface);

            var boost = loaded.Widgets[WidgetId.Boost.ToString()];
            Assert.False(boost.Visible);
            Assert.Equal(360, boost.X);
        }
        finally { File.Delete(path); }
    }
}
