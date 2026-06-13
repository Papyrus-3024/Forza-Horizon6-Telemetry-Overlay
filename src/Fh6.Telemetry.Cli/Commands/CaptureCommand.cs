using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class CaptureCommand : Command<CaptureCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("UDP port to listen on.")]
        [DefaultValue(20440)]
        public int Port { get; init; }

        [CommandOption("-o|--out")]
        [Description("Output file. Defaults to capture-<unixms>.jsonl.")]
        public string? Out { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var outPath = settings.Out ?? $"capture-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jsonl";

        using var source = new UdpTelemetrySource(settings.Port);
        using var writer = new JsonlCaptureWriter(outPath);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            source.Dispose(); // unblocks Receive(), ends the loop
        };

        AnsiConsole.MarkupLine($"[green]Listening on UDP :{settings.Port}[/] -> {Markup.Escape(outPath)}  (Ctrl-C to stop)");

        long count = 0;
        foreach (var frame in source.Frames())
        {
            writer.Write(frame.TimestampMs, frame.Data);
            count++;
            if (count % 60 == 0)
                AnsiConsole.Markup($"\r{count} packets ");
        }

        writer.Flush();
        AnsiConsole.MarkupLine($"\n[green]Saved {count} packets to {Markup.Escape(outPath)}[/]");
        return 0;
    }
}
