using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Settings;

namespace Fh6.Telemetry.Tests;

public class MapTransformTests
{
    // ── WorldToMap ────────────────────────────────────────────────────────────

    [Fact]
    public void ToPixel_applies_affine_correctly()
    {
        var c = new MapCalibration { A = 2.0, B = 0.5, C = 100.0, D = -0.5, E = 3.0, F = 200.0 };
        var (px, py) = WorldToMap.ToPixel(10.0, 20.0, c);
        // pixelX = 2*10 + 0.5*20 + 100 = 20 + 10 + 100 = 130
        // pixelY = -0.5*10 + 3*20 + 200 = -5 + 60 + 200 = 255
        Assert.Equal(130.0, px, 9);
        Assert.Equal(255.0, py, 9);
    }

    [Fact]
    public void ToPixel_identity_calibration_returns_world_coords()
    {
        var c = MapCalibration.Identity;
        var (px, py) = WorldToMap.ToPixel(42.0, 77.0, c);
        Assert.Equal(42.0, px, 9);
        Assert.Equal(77.0, py, 9);
    }

    // ── AffineFit ─────────────────────────────────────────────────────────────

    private static readonly MapCalibration ReferenceCalibration = new()
    {
        A = 0.65, B = 0.0007, C = 10387.0,
        D = -0.0037, E = -0.657, F = 9846.0,
    };

    private static (double worldX, double worldZ, double pixelX, double pixelY) MakePoint(
        double wx, double wz, MapCalibration c)
    {
        var (px, py) = WorldToMap.ToPixel(wx, wz, c);
        return (wx, wz, px, py);
    }

    [Fact]
    public void Fit_recovers_known_calibration_from_4_points()
    {
        var cal = ReferenceCalibration;
        var points = new[]
        {
            MakePoint(0.0,    0.0,    cal),
            MakePoint(1000.0, 0.0,    cal),
            MakePoint(0.0,    1000.0, cal),
            MakePoint(500.0,  750.0,  cal),
        };

        var fit = AffineFit.Fit(points);

        const double tol = 1e-3;
        Assert.Equal(cal.A, fit.A, tol);
        Assert.Equal(cal.B, fit.B, tol);
        Assert.Equal(cal.C, fit.C, tol);
        Assert.Equal(cal.D, fit.D, tol);
        Assert.Equal(cal.E, fit.E, tol);
        Assert.Equal(cal.F, fit.F, tol);
    }

    [Fact]
    public void Fit_round_trips_pixel_coords_within_tolerance()
    {
        var cal = ReferenceCalibration;
        var points = new[]
        {
            MakePoint(200.0,  300.0, cal),
            MakePoint(800.0,  100.0, cal),
            MakePoint(400.0,  900.0, cal),
            MakePoint(650.0,  450.0, cal),
        };

        var fit = AffineFit.Fit(points);

        foreach (var (wx, wz, expPx, expPy) in points)
        {
            var (gotPx, gotPy) = WorldToMap.ToPixel(wx, wz, fit);
            Assert.Equal(expPx, gotPx, 1e-3);
            Assert.Equal(expPy, gotPy, 1e-3);
        }
    }

    [Fact]
    public void Fit_exact_3_point_pure_scale_and_offset()
    {
        // Simple known transform: pixelX = 2*worldX + 50, pixelY = 3*worldZ + 75
        // B=0, D=0
        var points = new (double, double, double, double)[]
        {
            (0.0,   0.0,   50.0,  75.0),
            (100.0, 0.0,   250.0, 75.0),
            (0.0,   100.0, 50.0,  375.0),
        };

        var fit = AffineFit.Fit(points);

        Assert.Equal(2.0,  fit.A, 1e-9);
        Assert.Equal(0.0,  fit.B, 1e-9);
        Assert.Equal(50.0, fit.C, 1e-9);
        Assert.Equal(0.0,  fit.D, 1e-9);
        Assert.Equal(3.0,  fit.E, 1e-9);
        Assert.Equal(75.0, fit.F, 1e-9);
    }

    [Fact]
    public void Fit_throws_on_fewer_than_3_points()
    {
        var twoPoints = new (double, double, double, double)[]
        {
            (0.0, 0.0, 0.0, 0.0),
            (1.0, 0.0, 1.0, 0.0),
        };

        Assert.Throws<ArgumentException>(() => AffineFit.Fit(twoPoints));
    }

    [Fact]
    public void Fit_throws_on_empty_list()
    {
        Assert.Throws<ArgumentException>(
            () => AffineFit.Fit(Array.Empty<(double, double, double, double)>()));
    }

    // ── OverlayConfig round-trip ──────────────────────────────────────────────

    [Fact]
    public void OverlayConfig_with_map_calibration_round_trips_via_config_store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6map-{Guid.NewGuid():N}.json");
        try
        {
            var cal = new MapCalibration
            {
                A = 0.65, B = 0.0007, C = 10387.0,
                D = -0.0037, E = -0.657, F = 9846.0,
            };
            var config = new OverlayConfig
            {
                MapImagePath   = @"C:\maps\prague.png",
                MapCalibration = cal,
            };

            ConfigStore.Save(path, config);
            var loaded = ConfigStore.Load(path);

            Assert.Equal(@"C:\maps\prague.png", loaded.MapImagePath);
            Assert.NotNull(loaded.MapCalibration);
            Assert.Equal(cal.A, loaded.MapCalibration!.A, 1e-9);
            Assert.Equal(cal.B, loaded.MapCalibration!.B, 1e-9);
            Assert.Equal(cal.C, loaded.MapCalibration!.C, 1e-9);
            Assert.Equal(cal.D, loaded.MapCalibration!.D, 1e-9);
            Assert.Equal(cal.E, loaded.MapCalibration!.E, 1e-9);
            Assert.Equal(cal.F, loaded.MapCalibration!.F, 1e-9);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OverlayConfig_null_map_fields_round_trip_as_null()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fh6map-null-{Guid.NewGuid():N}.json");
        try
        {
            var config = new OverlayConfig { MapImagePath = null, MapCalibration = null };
            ConfigStore.Save(path, config);
            var loaded = ConfigStore.Load(path);
            Assert.Null(loaded.MapImagePath);
            Assert.Null(loaded.MapCalibration);
        }
        finally { File.Delete(path); }
    }
}
