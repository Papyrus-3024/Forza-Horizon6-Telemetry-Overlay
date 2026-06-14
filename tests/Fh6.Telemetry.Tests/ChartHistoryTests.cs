using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class ChartHistoryTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static TelemetryReadout Racing(float throttle = 0f, float brake = 0f,
        float speedKmh = 0f, float rpmFraction = 0f)
    {
        // Build a minimal packet that produces IsRaceOn=true with specified values.
        var packet = new TelemetryPacket
        {
            IsRaceOn   = 1,
            Accel      = (byte)Math.Round(throttle * 255f),
            Brake      = (byte)Math.Round(brake    * 255f),
            Speed      = speedKmh / 3.6f,
            EngineMaxRpm    = rpmFraction > 0f ? 10000f : 0f,
            CurrentEngineRpm = rpmFraction > 0f ? rpmFraction * 10000f : 0f,
        };
        return new TelemetryReadout(packet);
    }

    private static TelemetryReadout NotRacing() =>
        new(new TelemetryPacket { IsRaceOn = 0 });

    // ── Count / Capacity ────────────────────────────────────────────────────

    [Fact]
    public void Count_starts_at_zero()
    {
        var h = new ChartHistory(10);
        Assert.Equal(0, h.Count);
        Assert.Equal(10, h.Capacity);
    }

    [Fact]
    public void Count_increments_up_to_Capacity()
    {
        var h = new ChartHistory(5);
        var r = Racing();
        for (int i = 0; i < 5; i++)
        {
            h.Add(r, (uint)i);
            Assert.Equal(i + 1, h.Count);
        }
    }

    [Fact]
    public void Count_caps_at_Capacity_when_overflowed()
    {
        var h = new ChartHistory(4);
        var r = Racing();
        for (int i = 0; i < 10; i++)
            h.Add(r, (uint)i);
        Assert.Equal(4, h.Count);
    }

    // ── Ring-overwrite ───────────────────────────────────────────────────────

    [Fact]
    public void Add_past_capacity_overwrites_oldest()
    {
        // Capacity 3, add 5 samples at t=0..4ms. After the 5 adds the visible window
        // should contain the 3 newest: t=2s (2000ms), 3s (3000ms), 4s (4000ms).
        var h = new ChartHistory(3);
        var r = Racing();
        for (uint t = 0; t < 5; t++)
            h.Add(r, t * 1000);

        var buf = new ChartSample[10];
        int n = h.CopyWindow(double.MaxValue, buf);

        Assert.Equal(3, n);
        Assert.Equal(2.0, buf[0].TimeSeconds, 6);
        Assert.Equal(3.0, buf[1].TimeSeconds, 6);
        Assert.Equal(4.0, buf[2].TimeSeconds, 6);
    }

    // ── TimeSeconds from TimestampMs deltas ─────────────────────────────────

    [Fact]
    public void TimeSeconds_uses_first_sample_as_origin()
    {
        var h = new ChartHistory(5);
        var r = Racing();
        h.Add(r, 5000);  // t=0s
        h.Add(r, 5500);  // t=0.5s
        h.Add(r, 6000);  // t=1.0s

        var buf = new ChartSample[5];
        int n = h.CopyWindow(double.MaxValue, buf);

        Assert.Equal(3, n);
        Assert.Equal(0.0, buf[0].TimeSeconds, 6);
        Assert.Equal(0.5, buf[1].TimeSeconds, 6);
        Assert.Equal(1.0, buf[2].TimeSeconds, 6);
    }

    [Fact]
    public void TimeSeconds_correct_for_non_uniform_intervals()
    {
        // Simulate dropped packets: gaps of 16ms, 50ms, 33ms
        var h = new ChartHistory(10);
        var r = Racing();
        uint[] stamps = { 1000, 1016, 1066, 1099 };
        foreach (var t in stamps)
            h.Add(r, t);

        var buf = new ChartSample[10];
        int n = h.CopyWindow(double.MaxValue, buf);

        Assert.Equal(4, n);
        Assert.Equal(0.000, buf[0].TimeSeconds, 6);
        Assert.Equal(0.016, buf[1].TimeSeconds, 6);
        Assert.Equal(0.066, buf[2].TimeSeconds, 6);
        Assert.Equal(0.099, buf[3].TimeSeconds, 6);
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_clears_buffer_and_Count()
    {
        var h = new ChartHistory(5);
        var r = Racing();
        h.Add(r, 0); h.Add(r, 100); h.Add(r, 200);
        h.Reset();

        Assert.Equal(0, h.Count);
        var buf = new ChartSample[5];
        Assert.Equal(0, h.CopyWindow(double.MaxValue, buf));
    }

    [Fact]
    public void Reset_restarts_clock_at_zero()
    {
        var h = new ChartHistory(5);
        var r = Racing();
        h.Add(r, 9000);
        h.Add(r, 9500);
        h.Reset();

        // After reset, first sample should be TimeSeconds == 0 regardless of the new timestamp.
        h.Add(r, 20000);
        h.Add(r, 21000);

        var buf = new ChartSample[5];
        int n = h.CopyWindow(double.MaxValue, buf);

        Assert.Equal(2, n);
        Assert.Equal(0.0, buf[0].TimeSeconds, 6);
        Assert.Equal(1.0, buf[1].TimeSeconds, 6);
    }

    // ── CopyWindow ───────────────────────────────────────────────────────────

    [Fact]
    public void CopyWindow_returns_zero_when_empty()
    {
        var h = new ChartHistory(5);
        var buf = new ChartSample[5];
        Assert.Equal(0, h.CopyWindow(60.0, buf));
    }

    [Fact]
    public void CopyWindow_returns_all_when_window_larger_than_history()
    {
        var h = new ChartHistory(10);
        var r = Racing();
        for (uint i = 0; i < 5; i++)
            h.Add(r, i * 1000);

        var buf = new ChartSample[10];
        int n = h.CopyWindow(100.0, buf);
        Assert.Equal(5, n);
    }

    [Fact]
    public void CopyWindow_filters_samples_outside_window()
    {
        // 10 samples at 0s..9s; request 3s window → only 3s, 4s..9s → the last 7 samples,
        // but the window is "latest - 3" so latest=9, cutoff=6 → samples at t≥6 → 4 samples (6,7,8,9).
        var h = new ChartHistory(20);
        var r = Racing();
        for (uint i = 0; i < 10; i++)
            h.Add(r, i * 1000);

        var buf = new ChartSample[20];
        int n = h.CopyWindow(3.0, buf);

        // t=6,7,8,9 → 4 samples
        Assert.Equal(4, n);
        Assert.Equal(6.0, buf[0].TimeSeconds, 6);
        Assert.Equal(9.0, buf[3].TimeSeconds, 6);
    }

    [Fact]
    public void CopyWindow_returns_oldest_first()
    {
        var h = new ChartHistory(10);
        var r = Racing();
        for (uint i = 0; i < 5; i++)
            h.Add(r, i * 500);  // 0, 0.5, 1.0, 1.5, 2.0 s

        var buf = new ChartSample[10];
        int n = h.CopyWindow(double.MaxValue, buf);

        for (int i = 1; i < n; i++)
            Assert.True(buf[i].TimeSeconds > buf[i - 1].TimeSeconds,
                "Samples should be oldest-first (monotonically increasing TimeSeconds)");
    }

    [Fact]
    public void CopyWindow_is_bounded_by_dest_length()
    {
        var h = new ChartHistory(20);
        var r = Racing();
        for (uint i = 0; i < 10; i++)
            h.Add(r, i * 1000);

        var buf = new ChartSample[3];  // only 3 slots
        int n = h.CopyWindow(double.MaxValue, buf);
        Assert.Equal(3, n);
    }

    [Fact]
    public void CopyWindow_correct_at_full_capacity()
    {
        // Fill exactly to capacity, no overflow.
        var h = new ChartHistory(6);
        var r = Racing();
        for (uint i = 0; i < 6; i++)
            h.Add(r, i * 1000);

        var buf = new ChartSample[10];
        int n = h.CopyWindow(double.MaxValue, buf);
        Assert.Equal(6, n);
        Assert.Equal(0.0, buf[0].TimeSeconds, 6);
        Assert.Equal(5.0, buf[5].TimeSeconds, 6);
    }

    [Fact]
    public void CopyWindow_correct_after_ring_overflow()
    {
        // Capacity 4, add 7 samples. The ring holds the newest 4 (t=3..6).
        // With a window of 2s: cutoff = 6 - 2 = 4, so t≥4 → 3 samples (4, 5, 6).
        var h = new ChartHistory(4);
        var r = Racing();
        for (uint i = 0; i < 7; i++)
            h.Add(r, i * 1000);

        var buf = new ChartSample[10];
        int n = h.CopyWindow(2.0, buf);

        Assert.Equal(3, n);
        Assert.Equal(4.0, buf[0].TimeSeconds, 6);
        Assert.Equal(5.0, buf[1].TimeSeconds, 6);
        Assert.Equal(6.0, buf[2].TimeSeconds, 6);
    }

    // ── Channel values ───────────────────────────────────────────────────────

    [Fact]
    public void Stored_channel_values_match_readout()
    {
        var packet = new TelemetryPacket
        {
            IsRaceOn         = 1,
            Accel            = 128,
            Brake            = 64,
            Speed            = 50f / 3.6f,
            EngineMaxRpm     = 8000f,
            CurrentEngineRpm = 4000f,
            Gear             = 3,
        };
        var r = new TelemetryReadout(packet);

        var h = new ChartHistory(5);
        h.Add(r, 0u);

        var buf = new ChartSample[5];
        h.CopyWindow(double.MaxValue, buf);

        Assert.Equal(r.ThrottleFraction, buf[0].Throttle, 4);
        Assert.Equal(r.BrakeFraction,    buf[0].Brake,    4);
        Assert.Equal(r.SpeedKmh,         buf[0].SpeedKmh, 2);
        Assert.Equal(r.RpmFraction,      buf[0].RpmFraction, 4);
        Assert.Equal(3,                  buf[0].Gear);
    }
}
