using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Settings;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class ChartMathTests
{
    // ── ChartMath.Normalize ─────────────────────────────────────────────────

    [Fact]
    public void Normalize_midpoint_returns_half()
    {
        Assert.Equal(0.5f, ChartMath.Normalize(0.5f, 0f, 1f), 6);
    }

    [Fact]
    public void Normalize_at_min_returns_zero()
    {
        Assert.Equal(0f, ChartMath.Normalize(0f, 0f, 100f));
    }

    [Fact]
    public void Normalize_at_max_returns_one()
    {
        Assert.Equal(1f, ChartMath.Normalize(100f, 0f, 100f));
    }

    [Fact]
    public void Normalize_below_min_clamps_to_zero()
    {
        Assert.Equal(0f, ChartMath.Normalize(-10f, 0f, 100f));
    }

    [Fact]
    public void Normalize_above_max_clamps_to_one()
    {
        Assert.Equal(1f, ChartMath.Normalize(200f, 0f, 100f));
    }

    [Fact]
    public void Normalize_steer_centered_range_zero_gives_half()
    {
        // Steer uses -1..1; v=0 should map to 0.5.
        Assert.Equal(0.5f, ChartMath.Normalize(0f, -1f, 1f), 6);
    }

    [Fact]
    public void Normalize_steer_minus_one_maps_to_zero()
    {
        Assert.Equal(0f, ChartMath.Normalize(-1f, -1f, 1f));
    }

    [Fact]
    public void Normalize_steer_plus_one_maps_to_one()
    {
        Assert.Equal(1f, ChartMath.Normalize(1f, -1f, 1f));
    }

    [Fact]
    public void Normalize_min_equals_max_returns_zero()
    {
        // Guard against divide-by-zero.
        Assert.Equal(0f, ChartMath.Normalize(42f, 42f, 42f));
    }

    // ── ChartMath.DecimateIndices ───────────────────────────────────────────

    [Fact]
    public void DecimateIndices_empty_input_returns_zero()
    {
        var dest = new int[10];
        Assert.Equal(0, ChartMath.DecimateIndices(ReadOnlySpan<double>.Empty, 5, dest));
    }

    [Fact]
    public void DecimateIndices_single_element_returns_one()
    {
        double[] xs = { 1.0 };
        var dest = new int[10];
        int n = ChartMath.DecimateIndices(xs, 5, dest);
        Assert.Equal(1, n);
        Assert.Equal(0, dest[0]);
    }

    [Fact]
    public void DecimateIndices_fewer_samples_than_columns_returns_all()
    {
        // 3 xs, 10 target columns → should get 3 distinct buckets → 3 indices.
        double[] xs = { 0.0, 1.0, 2.0 };
        var dest = new int[10];
        int n = ChartMath.DecimateIndices(xs, 10, dest);
        Assert.Equal(3, n);
        Assert.Equal(0, dest[0]);
        Assert.Equal(1, dest[1]);
        Assert.Equal(2, dest[2]);
    }

    [Fact]
    public void DecimateIndices_reduces_to_target_columns()
    {
        // 100 evenly-spaced xs → 10 buckets → should emit ≤ 10 indices.
        double[] xs = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();
        var dest = new int[100];
        int n = ChartMath.DecimateIndices(xs, 10, dest);
        Assert.True(n <= 10, $"Expected ≤ 10 indices, got {n}");
        Assert.True(n > 0);
    }

    [Fact]
    public void DecimateIndices_result_indices_are_monotonically_increasing()
    {
        double[] xs = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();
        var dest = new int[50];
        int n = ChartMath.DecimateIndices(xs, 8, dest);
        for (int i = 1; i < n; i++)
            Assert.True(dest[i] > dest[i - 1], "Indices must be strictly increasing");
    }

    [Fact]
    public void DecimateIndices_respects_dest_length_cap()
    {
        double[] xs = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();
        var dest = new int[3];  // only 3 slots
        int n = ChartMath.DecimateIndices(xs, 100, dest);
        Assert.Equal(3, n);
    }

    [Fact]
    public void DecimateIndices_all_same_x_returns_one()
    {
        // When all X values are identical the range is 0; should return a single bucket.
        double[] xs = { 5.0, 5.0, 5.0, 5.0 };
        var dest = new int[10];
        int n = ChartMath.DecimateIndices(xs, 4, dest);
        Assert.Equal(1, n);
    }

    // ── ChartMath.DecimateMinMaxIndices ────────────────────────────────────

    [Fact]
    public void DecimateMinMaxIndices_empty_input_returns_zero()
    {
        var dest = new int[10];
        Assert.Equal(0, ChartMath.DecimateMinMaxIndices(
            ReadOnlySpan<double>.Empty, ReadOnlySpan<float>.Empty, 5, dest));
    }

    [Fact]
    public void DecimateMinMaxIndices_single_element_returns_one()
    {
        double[] xs = { 0.0 };
        float[] vs = { 42f };
        var dest = new int[10];
        int n = ChartMath.DecimateMinMaxIndices(xs, vs, 5, dest);
        Assert.Equal(1, n);
        Assert.Equal(0, dest[0]);
    }

    [Fact]
    public void DecimateMinMaxIndices_two_in_same_bucket_emits_min_and_max()
    {
        // Both samples fall in the same bucket; min is index 1 (value 1), max is index 0 (value 10).
        double[] xs = { 0.0, 0.1 };   // range tiny → same bucket
        float[]  vs = { 10f, 1f };
        var dest = new int[10];
        int n = ChartMath.DecimateMinMaxIndices(xs, vs, 1, dest);
        // One bucket → emits both min and max indices in X order (index 0 < index 1).
        Assert.Equal(2, n);
        // Max is at index 0, min at index 1; emitted in X order: 0 then 1.
        Assert.Equal(0, dest[0]);
        Assert.Equal(1, dest[1]);
    }

    [Fact]
    public void DecimateMinMaxIndices_same_min_and_max_emits_one_per_bucket()
    {
        // Single unique value per bucket → min == max → only one index emitted.
        double[] xs = { 0.0, 5.0, 10.0 };
        float[]  vs = { 1f,  2f,  3f };
        var dest = new int[10];
        int n = ChartMath.DecimateMinMaxIndices(xs, vs, 3, dest);
        // 3 samples, 3 buckets, each with a single sample → 3 indices.
        Assert.Equal(3, n);
    }

    [Fact]
    public void DecimateMinMaxIndices_result_indices_are_monotonically_increasing()
    {
        // Fill 100 samples alternating high/low to exercise min/max within buckets.
        int len = 100;
        double[] xs = Enumerable.Range(0, len).Select(i => (double)i).ToArray();
        float[]  vs = Enumerable.Range(0, len).Select(i => i % 2 == 0 ? 1f : 0f).ToArray();
        var dest = new int[len * 2];
        int n = ChartMath.DecimateMinMaxIndices(xs, vs, 10, dest);
        Assert.True(n > 0);
        for (int i = 1; i < n; i++)
            Assert.True(dest[i] > dest[i - 1], $"Indices must be strictly increasing (failed at {i})");
    }

    [Fact]
    public void DecimateMinMaxIndices_mismatched_lengths_returns_zero()
    {
        double[] xs = { 0.0, 1.0 };
        float[]  vs = { 1f };   // length mismatch
        var dest = new int[10];
        Assert.Equal(0, ChartMath.DecimateMinMaxIndices(xs, vs, 5, dest));
    }

    // ── ChartConfig.Normalize ───────────────────────────────────────────────

    [Theory]
    [InlineData(30.0,  30.0)]   // exact match
    [InlineData(60.0,  60.0)]   // exact match
    [InlineData(120.0, 120.0)]  // exact match
    [InlineData(45.0,  30.0)]   // closer to 30 than to 60? 45-30=15, 60-45=15 → tie, pick 30 (first found)
    [InlineData(50.0,  60.0)]   // closer to 60
    [InlineData(0.0,   30.0)]   // below range → nearest is 30
    [InlineData(200.0, 120.0)]  // above range → nearest is 120
    public void ChartConfig_Normalize_clamps_window_to_supported_steps(double input, double expected)
    {
        var cfg = new ChartConfig { WindowSeconds = input };
        cfg.Normalize();
        Assert.Equal(expected, cfg.WindowSeconds);
    }

    [Fact]
    public void ChartConfig_Normalize_unknown_series_keys_are_ignored()
    {
        var cfg = new ChartConfig { WindowSeconds = 60.0 };
        cfg.Series["Throttle"] = true;
        cfg.Series["NonExistentSeries"] = false;  // not in ChartSeriesId but must not throw

        var ex = Record.Exception(() => cfg.Normalize());
        Assert.Null(ex);

        // Keys are preserved as-is (Normalize does not strip unknown keys)
        Assert.True(cfg.Series.ContainsKey("NonExistentSeries"));
    }

    [Fact]
    public void ChartConfig_Normalize_leaves_series_dictionary_unchanged()
    {
        var cfg = new ChartConfig { WindowSeconds = 60.0 };
        cfg.Series["Brake"] = false;
        cfg.Series["Speed"] = true;
        cfg.Normalize();

        Assert.False(cfg.Series["Brake"]);
        Assert.True(cfg.Series["Speed"]);
    }
}
