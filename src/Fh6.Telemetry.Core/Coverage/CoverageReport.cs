namespace Fh6.Telemetry.Core.Coverage;

public readonly record struct CoverageItem(string Name, bool Met, long? FirstFrame);

public sealed class CoverageReport
{
    public CoverageReport(IReadOnlyList<CoverageItem> items) => Items = items;

    public IReadOnlyList<CoverageItem> Items { get; }

    public bool Complete => Items.All(i => i.Met);
}
