using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Tests;

public class ChartSeriesCatalogTests
{
    // ── All ──────────────────────────────────────────────────────────────────

    [Fact]
    public void All_contains_every_ChartSeriesId()
    {
        var ids = Enum.GetValues<ChartSeriesId>().ToHashSet();
        foreach (var def in ChartSeriesCatalog.All)
            ids.Remove(def.Id);
        Assert.Empty(ids);
    }

    [Fact]
    public void All_count_matches_ChartSeriesId_count()
    {
        int enumCount = Enum.GetValues<ChartSeriesId>().Length;
        Assert.Equal(enumCount, ChartSeriesCatalog.All.Count);
    }

    [Fact]
    public void All_has_no_duplicate_ids()
    {
        var seen = new HashSet<ChartSeriesId>();
        foreach (var def in ChartSeriesCatalog.All)
            Assert.True(seen.Add(def.Id), $"Duplicate id: {def.Id}");
    }

    [Fact]
    public void All_entries_have_non_empty_names()
    {
        foreach (var def in ChartSeriesCatalog.All)
            Assert.False(string.IsNullOrWhiteSpace(def.Name), $"Empty name for {def.Id}");
    }

    [Fact]
    public void All_entries_have_valid_ranges()
    {
        foreach (var def in ChartSeriesCatalog.All)
            Assert.True(def.Max >= def.Min, $"{def.Id}: Max < Min");
    }

    // ── DefaultOn ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChartSeriesId.Throttle, true)]
    [InlineData(ChartSeriesId.Brake,    true)]
    [InlineData(ChartSeriesId.Speed,    true)]
    [InlineData(ChartSeriesId.Clutch,   false)]
    [InlineData(ChartSeriesId.Steer,    false)]
    [InlineData(ChartSeriesId.Rpm,      false)]
    [InlineData(ChartSeriesId.Gear,     false)]
    [InlineData(ChartSeriesId.Power,    false)]
    [InlineData(ChartSeriesId.Torque,   false)]
    [InlineData(ChartSeriesId.LatG,     false)]
    [InlineData(ChartSeriesId.LongG,    false)]
    public void DefaultOn_returns_correct_value(ChartSeriesId id, bool expected)
    {
        Assert.Equal(expected, ChartSeriesCatalog.DefaultOn(id));
    }

    // ── IsEnabled ────────────────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_absent_key_falls_back_to_DefaultOn()
    {
        var cfg = new ChartConfig(); // empty Series dict

        foreach (ChartSeriesId id in Enum.GetValues<ChartSeriesId>())
            Assert.Equal(ChartSeriesCatalog.DefaultOn(id), ChartSeriesCatalog.IsEnabled(cfg, id));
    }

    [Fact]
    public void IsEnabled_explicit_true_overrides_DefaultOn()
    {
        var cfg = new ChartConfig();
        // Steer is default-off; explicitly enable it.
        cfg.Series[ChartSeriesId.Steer.ToString()] = true;
        Assert.True(ChartSeriesCatalog.IsEnabled(cfg, ChartSeriesId.Steer));
    }

    [Fact]
    public void IsEnabled_explicit_false_overrides_DefaultOn()
    {
        var cfg = new ChartConfig();
        // Throttle is default-on; explicitly disable it.
        cfg.Series[ChartSeriesId.Throttle.ToString()] = false;
        Assert.False(ChartSeriesCatalog.IsEnabled(cfg, ChartSeriesId.Throttle));
    }

    [Fact]
    public void IsEnabled_unknown_key_in_series_dict_is_ignored()
    {
        var cfg = new ChartConfig();
        cfg.Series["UnknownSeries"] = true;
        // Should not throw; Throttle still uses its default.
        Assert.True(ChartSeriesCatalog.IsEnabled(cfg, ChartSeriesId.Throttle));
    }

    // ── Select / FormatValue ─────────────────────────────────────────────────

    [Fact]
    public void Select_and_FormatValue_do_not_throw_on_zero_sample()
    {
        var sample = new Fh6.Telemetry.Core.ChartSample(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        foreach (var def in ChartSeriesCatalog.All)
        {
            var raw = def.Select(sample);
            var text = def.FormatValue(raw);
            Assert.False(string.IsNullOrEmpty(text), $"Empty format for {def.Id}");
        }
    }

    // ── ChartConfig.Normalize ────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0,   30.0)]  // below minimum → snap to 30
    [InlineData(30.0,  30.0)]  // exact match
    [InlineData(45.0,  30.0)]  // closest to 30 (30 vs 60; equidistant picks first)
    [InlineData(46.0,  60.0)]  // closer to 60
    [InlineData(60.0,  60.0)]  // exact match
    [InlineData(90.0,  60.0)]  // equidistant 60/120 → picks whichever comes first in array
    [InlineData(91.0, 120.0)]  // closer to 120
    [InlineData(200.0, 120.0)] // above maximum → snap to 120
    public void ChartConfig_Normalize_clamps_window_to_nearest_supported(double input, double expected)
    {
        var cfg = new ChartConfig { WindowSeconds = input };
        cfg.Normalize();
        Assert.Equal(expected, cfg.WindowSeconds);
    }

    [Fact]
    public void ChartConfig_Normalize_leaves_valid_series_keys_intact()
    {
        var cfg = new ChartConfig { WindowSeconds = 60 };
        cfg.Series[ChartSeriesId.Throttle.ToString()] = false;
        cfg.Normalize();
        // The explicit override must survive normalize.
        Assert.False(cfg.Series[ChartSeriesId.Throttle.ToString()]);
    }
}
