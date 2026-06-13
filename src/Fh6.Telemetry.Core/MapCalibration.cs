namespace Fh6.Telemetry.Core;

/// <summary>
/// Six-parameter affine transform mapping world (X, Z) to map pixel (x, y):
///   pixelX = A*worldX + B*worldZ + C
///   pixelY = D*worldX + E*worldZ + F
/// </summary>
public sealed class MapCalibration
{
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }
    public double D { get; set; }
    public double E { get; set; }
    public double F { get; set; }

    /// <summary>Identity transform: pixelX = worldX, pixelY = worldZ.</summary>
    public static MapCalibration Identity => new() { A = 1.0, B = 0.0, C = 0.0, D = 0.0, E = 1.0, F = 0.0 };
}
