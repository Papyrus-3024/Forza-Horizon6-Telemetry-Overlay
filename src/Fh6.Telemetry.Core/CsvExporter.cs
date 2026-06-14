using System.Globalization;

namespace Fh6.Telemetry.Core;

/// <summary>
/// Projects captured packets to a flat CSV with one row per packet, for offline analysis
/// (e.g. pandas). Column names are snake_case and values use invariant culture so the file
/// parses identically regardless of the machine's locale.
/// </summary>
public static class CsvExporter
{
    private static readonly string[] Header =
    {
        "timestamp_ms", "speed_mph", "speed_kmh", "gear", "rpm", "max_rpm", "rpm_frac",
        "throttle", "brake", "clutch", "steer", "lat_g", "long_g",
        "power", "torque", "boost", "fuel_pct",
        "tire_temp_fl", "tire_temp_fr", "tire_temp_rl", "tire_temp_rr",
        "slip_fl", "slip_fr", "slip_rl", "slip_rr",
        "susp_fl", "susp_fr", "susp_rl", "susp_rr",
        "pos_x", "pos_z", "lap", "position", "best_lap", "last_lap", "current_lap",
    };

    /// <summary>
    /// Writes a CSV (header + one row per parseable frame) to <paramref name="writer"/>.
    /// Returns the number of data rows written.
    /// </summary>
    public static int Export(IEnumerable<CaptureFrame> frames, TextWriter writer)
    {
        writer.Write(string.Join(',', Header));
        writer.Write('\n');

        var rows = 0;
        foreach (var frame in frames)
        {
            if (!PacketParser.TryParse(frame.Data, out var packet))
                continue;

            var r = new TelemetryReadout(packet);
            var fields = new[]
            {
                F(frame.TimestampMs), F(r.SpeedMs * 2.23694f), F(r.SpeedKmh), I(r.Gear),
                F(r.Rpm), F(r.MaxRpm), F(r.RpmFraction),
                F(r.ThrottleFraction), F(r.BrakeFraction), F(r.ClutchFraction), F(r.SteerFraction),
                F(r.LatG), F(r.LongG), F(r.Power), F(r.Torque), F(r.Boost), F(r.FuelPercent),
                F(r.TireTemp.FrontLeft), F(r.TireTemp.FrontRight), F(r.TireTemp.RearLeft), F(r.TireTemp.RearRight),
                F(r.TireCombinedSlip.FrontLeft), F(r.TireCombinedSlip.FrontRight),
                F(r.TireCombinedSlip.RearLeft), F(r.TireCombinedSlip.RearRight),
                F(r.SuspensionTravelNorm.FrontLeft), F(r.SuspensionTravelNorm.FrontRight),
                F(r.SuspensionTravelNorm.RearLeft), F(r.SuspensionTravelNorm.RearRight),
                F(r.PositionX), F(r.PositionZ), I(r.LapNumber), I(r.RacePosition),
                F(r.BestLap), F(r.LastLap), F(r.CurrentLap),
            };
            writer.Write(string.Join(',', fields));
            writer.Write('\n');
            rows++;
        }

        return rows;
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
}
