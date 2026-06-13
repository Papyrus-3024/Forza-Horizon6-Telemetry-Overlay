using Fh6.Telemetry.Core;
using Fh6.Telemetry.Core.Coverage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class CoverageCommand : Command<CoverageCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        public string File { get; init; } = "";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var source = new JsonlReplaySource(settings.File);
        var tracker = new CoverageTracker();

        long frames = 0;
        foreach (var frame in source.Frames())
        {
            if (PacketParser.TryParse(frame.Data, out var packet))
            {
                tracker.Observe(packet);
                frames++;
            }
        }

        var report = tracker.Report();
        var table = new Table()
            .AddColumn("Condition")
            .AddColumn("Status")
            .AddColumn("First frame");

        foreach (var item in report.Items)
        {
            table.AddRow(
                Markup.Escape(item.Name),
                item.Met ? "[green]met[/]" : "[red]missing[/]",
                item.FirstFrame?.ToString() ?? "-");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(report.Complete
            ? $"[green]Coverage complete[/] over {frames} frames."
            : $"[yellow]Coverage incomplete[/] over {frames} frames.");

        return report.Complete ? 0 : 1;
    }
}
