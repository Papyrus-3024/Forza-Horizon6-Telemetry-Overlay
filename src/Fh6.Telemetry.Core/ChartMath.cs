namespace Fh6.Telemetry.Core;

/// <summary>Pure, allocation-free math helpers for the chart renderer.</summary>
public static class ChartMath
{
    /// <summary>
    /// Maps <paramref name="v"/> from [<paramref name="min"/>..<paramref name="max"/>] to [0..1],
    /// clamping at both ends. Returns 0 when <paramref name="min"/> == <paramref name="max"/>.
    /// </summary>
    public static float Normalize(float v, float min, float max)
    {
        if (min == max) return 0f;
        return Math.Clamp((v - min) / (max - min), 0f, 1f);
    }

    /// <summary>
    /// Buckets the X values in <paramref name="xs"/> into <paramref name="targetColumns"/> equal
    /// intervals and writes one representative index per non-empty bucket (the index of the
    /// first element in that bucket) into <paramref name="destIndices"/>.
    /// Returns the number of indices written (≤ <c>min(targetColumns, destIndices.Length)</c>).
    /// </summary>
    /// <remarks>
    /// This is the lightweight first-per-bucket decimation used when plotting the chart at a
    /// given pixel width. For spike preservation (min/max per bucket) call a double pass with this
    /// helper or use the renderer's own pass; this helper is kept minimal so it stays unit-testable.
    /// </remarks>
    public static int DecimateIndices(ReadOnlySpan<double> xs, int targetColumns, Span<int> destIndices)
    {
        if (xs.IsEmpty || targetColumns <= 0 || destIndices.IsEmpty) return 0;

        double xMin = xs[0];
        double xMax = xs[xs.Length - 1];
        double range = xMax - xMin;

        int written = 0;
        int lastBucket = -1;

        for (int i = 0; i < xs.Length; i++)
        {
            int bucket;
            if (range <= 0.0)
            {
                bucket = 0;
            }
            else
            {
                bucket = (int)((xs[i] - xMin) / range * targetColumns);
                if (bucket >= targetColumns) bucket = targetColumns - 1;
            }

            if (bucket != lastBucket)
            {
                if (written >= destIndices.Length) break;
                destIndices[written++] = i;
                lastBucket = bucket;
            }
        }

        return written;
    }
}
