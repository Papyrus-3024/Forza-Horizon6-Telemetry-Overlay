using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class LiveCommand : Command<LiveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("UDP port to listen on.")]
        [DefaultValue(20440)]
        public int Port { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        using var source = new UdpTelemetrySource(settings.Port);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            source.Dispose();
        };

        var dashboard = new SpectreDashboard();
        dashboard.Run(render =>
        {
            foreach (var frame in source.Frames())
                if (PacketParser.TryParse(frame.Data, out var packet))
                    render(packet);
        });

        return 0;
    }
}
