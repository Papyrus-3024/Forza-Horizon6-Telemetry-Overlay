namespace Fh6.Telemetry.Core;

/// <summary>Applies a <see cref="MapCalibration"/> affine transform to world coordinates.</summary>
public static class WorldToMap
{
    /// <summary>
    /// Returns the map-pixel position for a world (X, Z) ground-plane coordinate.
    ///   pixelX = A*worldX + B*worldZ + C
    ///   pixelY = D*worldX + E*worldZ + F
    /// </summary>
    public static (double X, double Y) ToPixel(double worldX, double worldZ, MapCalibration c)
        => (c.A * worldX + c.B * worldZ + c.C,
            c.D * worldX + c.E * worldZ + c.F);
}
