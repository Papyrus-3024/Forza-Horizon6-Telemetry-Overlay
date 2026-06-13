namespace Fh6.Telemetry.Core;

/// <summary>
/// Fits a <see cref="MapCalibration"/> affine transform to a set of world→pixel
/// correspondences using least-squares (two independent 3-parameter regressions).
/// </summary>
public static class AffineFit
{
    /// <summary>
    /// Fits an affine calibration to <paramref name="points"/>.
    /// Each point is (worldX, worldZ, pixelX, pixelY).
    /// Requires at least 3 non-collinear points; throws <see cref="ArgumentException"/>
    /// if fewer than 3 points are supplied.
    /// </summary>
    public static MapCalibration Fit(
        IReadOnlyList<(double worldX, double worldZ, double pixelX, double pixelY)> points)
    {
        if (points.Count < 3)
            throw new ArgumentException("At least 3 calibration points are required.", nameof(points));

        // Build normal equations MᵀM and Mᵀb for the system M·[A,B,C]ᵀ = pixelX
        // where each row of M is [worldX, worldZ, 1].
        // The same MᵀM applies to the second regression for [D,E,F] against pixelY.
        double mtm00 = 0, mtm01 = 0, mtm02 = 0;
        double              mtm11 = 0, mtm12 = 0;
        double                          mtm22 = 0;

        double bx0 = 0, bx1 = 0, bx2 = 0;  // Mᵀ·pixelX
        double by0 = 0, by1 = 0, by2 = 0;  // Mᵀ·pixelY

        foreach (var (wx, wz, px, py) in points)
        {
            mtm00 += wx * wx;
            mtm01 += wx * wz;
            mtm02 += wx;
            mtm11 += wz * wz;
            mtm12 += wz;
            mtm22 += 1.0;   // = n

            bx0 += wx * px;
            bx1 += wz * px;
            bx2 += px;

            by0 += wx * py;
            by1 += wz * py;
            by2 += py;
        }

        // MᵀM is symmetric; fill in upper-triangle mirrors for the solver.
        // Augmented matrix layout: [row0 | row1 | row2] each is 4 doubles.
        var solX = Solve3x3(
            mtm00, mtm01, mtm02, bx0,
            mtm01, mtm11, mtm12, bx1,
            mtm02, mtm12, mtm22, bx2);

        var solY = Solve3x3(
            mtm00, mtm01, mtm02, by0,
            mtm01, mtm11, mtm12, by1,
            mtm02, mtm12, mtm22, by2);

        return new MapCalibration
        {
            A = solX[0], B = solX[1], C = solX[2],
            D = solY[0], E = solY[1], F = solY[2],
        };
    }

    /// <summary>
    /// Solves a 3×3 linear system via partial-pivot Gaussian elimination.
    /// Augmented matrix is passed as three rows of four values each.
    /// Returns the three solution values [x0, x1, x2].
    /// </summary>
    private static double[] Solve3x3(
        double a00, double a01, double a02, double b0,
        double a10, double a11, double a12, double b1,
        double a20, double a21, double a22, double b2)
    {
        // Augmented rows
        double[][] m =
        [
            [a00, a01, a02, b0],
            [a10, a11, a12, b1],
            [a20, a21, a22, b2],
        ];

        // Forward elimination with partial pivoting
        for (int col = 0; col < 3; col++)
        {
            // Find pivot
            int pivot = col;
            double maxVal = Math.Abs(m[col][col]);
            for (int row = col + 1; row < 3; row++)
            {
                double v = Math.Abs(m[row][col]);
                if (v > maxVal) { maxVal = v; pivot = row; }
            }

            if (pivot != col)
            {
                var tmp = m[col];
                m[col] = m[pivot];
                m[pivot] = tmp;
            }

            double diag = m[col][col];
            if (Math.Abs(diag) < 1e-15)
                throw new InvalidOperationException(
                    "Calibration points are collinear or nearly collinear; cannot fit affine.");

            for (int row = col + 1; row < 3; row++)
            {
                double factor = m[row][col] / diag;
                for (int k = col; k < 4; k++)
                    m[row][k] -= factor * m[col][k];
            }
        }

        // Back substitution
        var x = new double[3];
        for (int row = 2; row >= 0; row--)
        {
            double sum = m[row][3];
            for (int k = row + 1; k < 3; k++)
                sum -= m[row][k] * x[k];
            x[row] = sum / m[row][row];
        }

        return x;
    }
}
