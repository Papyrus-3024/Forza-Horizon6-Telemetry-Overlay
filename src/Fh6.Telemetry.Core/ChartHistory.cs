namespace Fh6.Telemetry.Core;

/// <summary>
/// Fixed-capacity ring-buffer sampler for the chart widget.
/// Stateful companion to the stateless <see cref="TelemetryReadout"/>; reset when racing ends.
/// </summary>
public sealed class ChartHistory
{
    private readonly ChartSample[] _buf;
    private int _head;   // index of the next write slot
    private int _count;
    private double _t0Ms;
    private bool _hasT0;

    public ChartHistory(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buf = new ChartSample[capacity];
    }

    public int Count    => _count;
    public int Capacity => _buf.Length;

    /// <summary>
    /// Append one readout. <paramref name="timestampMs"/> drives the time axis via delta from the
    /// first sample's timestamp — never assumes 60 Hz or a zero-origin clock.
    /// </summary>
    public void Add(in TelemetryReadout r, uint timestampMs)
    {
        if (!_hasT0)
        {
            _t0Ms  = timestampMs;
            _hasT0 = true;
        }

        double timeSeconds = (timestampMs - _t0Ms) / 1000.0;

        _buf[_head] = new ChartSample(
            timeSeconds,
            r.ThrottleFraction, r.BrakeFraction, r.ClutchFraction, r.SteerFraction,
            r.SpeedKmh, r.RpmFraction, r.Gear,
            r.PowerHp, r.TorqueLbFt, r.LatG, r.LongG);

        _head = (_head + 1) % _buf.Length;
        if (_count < _buf.Length) _count++;
    }

    /// <summary>Clear the buffer and reset the time origin. Called on the <c>!IsRaceOn</c> falling edge.</summary>
    public void Reset()
    {
        _head   = 0;
        _count  = 0;
        _hasT0  = false;
    }

    /// <summary>
    /// Copies samples with <c>TimeSeconds &gt;= latestTime - windowSeconds</c> into
    /// <paramref name="dest"/>, oldest-first. Returns the number of samples written.
    /// The returned count is bounded by <c>dest.Length</c> (additional samples are silently dropped).
    /// </summary>
    public int CopyWindow(double windowSeconds, Span<ChartSample> dest)
    {
        if (_count == 0 || dest.IsEmpty) return 0;

        // Walk the ring oldest-first to find the latest sample's time.
        // The newest sample is at (_head - 1 + Capacity) % Capacity.
        int newestIndex = (_head - 1 + _buf.Length) % _buf.Length;
        double latestTime = _buf[newestIndex].TimeSeconds;
        double cutoff = latestTime - windowSeconds;

        int written = 0;
        // Oldest sample lives at (_head - _count + Capacity) % Capacity.
        int startIndex = (_head - _count + _buf.Length) % _buf.Length;

        for (int i = 0; i < _count; i++)
        {
            int idx = (startIndex + i) % _buf.Length;
            ref readonly ChartSample s = ref _buf[idx];
            if (s.TimeSeconds >= cutoff)
            {
                if (written >= dest.Length) break;
                dest[written++] = s;
            }
        }

        return written;
    }
}
