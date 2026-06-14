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

    /// <summary>
    /// Buckets the X values into <paramref name="targetColumns"/> equal intervals and, for each
    /// non-empty bucket, emits the index of the sample with the minimum value and the index of the
    /// sample with the maximum value (in X order: min-index first if it precedes max-index,
    /// otherwise max then min). When min and max happen to be the same sample only one index is
    /// emitted for that bucket.
    /// </summary>
    /// <param name="xs">Pixel-space X coordinates, oldest-first, same length as <paramref name="values"/>.</param>
    /// <param name="values">The channel values used to find min/max within each bucket.</param>
    /// <param name="targetColumns">Number of X-buckets (typically the plot width in pixels).</param>
    /// <param name="destIndices">Output buffer; must be ≥ 2 × <paramref name="targetColumns"/> to avoid truncation.</param>
    /// <returns>Number of indices written.</returns>
    public static int DecimateMinMaxIndices(
        ReadOnlySpan<double> xs,
        ReadOnlySpan<float> values,
        int targetColumns,
        Span<int> destIndices)
    {
        if (xs.IsEmpty || values.IsEmpty || xs.Length != values.Length
            || targetColumns <= 0 || destIndices.IsEmpty) return 0;

        double xMin = xs[0];
        double xMax = xs[xs.Length - 1];
        double range = xMax - xMin;

        int written = 0;

        // Walk buckets in order, collecting min/max indices per bucket.
        int i = 0;
        while (i < xs.Length)
        {
            // Determine which bucket sample i belongs to.
            int bucket;
            if (range <= 0.0)
                bucket = 0;
            else
            {
                bucket = (int)((xs[i] - xMin) / range * targetColumns);
                if (bucket >= targetColumns) bucket = targetColumns - 1;
            }

            // Scan forward to consume all samples in this bucket.
            int minIdx = i, maxIdx = i;
            float minVal = values[i], maxVal = values[i];
            i++;

            while (i < xs.Length)
            {
                int b;
                if (range <= 0.0) b = 0;
                else
                {
                    b = (int)((xs[i] - xMin) / range * targetColumns);
                    if (b >= targetColumns) b = targetColumns - 1;
                }
                if (b != bucket) break;

                float v = values[i];
                if (v < minVal) { minVal = v; minIdx = i; }
                if (v > maxVal) { maxVal = v; maxIdx = i; }
                i++;
            }

            // Emit min and max in X order (avoids zigzag lines).
            if (minIdx == maxIdx)
            {
                if (written >= destIndices.Length) break;
                destIndices[written++] = minIdx;
            }
            else if (minIdx < maxIdx)
            {
                if (written + 1 >= destIndices.Length) break;
                destIndices[written++] = minIdx;
                destIndices[written++] = maxIdx;
            }
            else
            {
                if (written + 1 >= destIndices.Length) break;
                destIndices[written++] = maxIdx;
                destIndices[written++] = minIdx;
            }
        }

        return written;
    }
}
