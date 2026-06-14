using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class ExportCommand : Command<ExportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to a JSONL capture file.")]
        public string File { get; init; } = "";

        [CommandOption("-o|--out")]
        [Description("Output CSV path. Defaults to the input path with a .csv extension.")]
        public string? Out { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(settings.File))
        {
            AnsiConsole.MarkupLine($"[red]Capture not found:[/] {settings.File}");
            return 1;
        }

        var outPath = settings.Out ?? System.IO.Path.ChangeExtension(settings.File, ".csv");
        var source = new JsonlReplaySource(settings.File);

        int rows;
        using (var writer = new StreamWriter(outPath, append: false))
            rows = CsvExporter.Export(source.Frames(), writer);

        AnsiConsole.MarkupLine($"[green]Exported[/] {rows} rows to {outPath}");
        return 0;
    }
}
