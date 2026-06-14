using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

/// <summary>
/// Connection diagnostics: binds the Data Out port, samples for a few seconds, and reports
/// packets/sec and packet validity. If nothing arrives, prints the manual checklist (the
/// remaining causes are game-side and not testable from here).
/// </summary>
public sealed class DoctorCommand : Command<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("UDP port FH6 Data Out targets.")]
        [DefaultValue(20440)]
        public int Port { get; init; }

        [CommandOption("-s|--seconds")]
        [Description("How long to sample for packets.")]
        [DefaultValue(5)]
        public int Seconds { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        AnsiConsole.MarkupLine($"[bold]FH6 connection doctor[/] — listening on 0.0.0.0:{settings.Port} for {settings.Seconds}s\n");

        UdpClient client;
        try
        {
            client = new UdpClient(new IPEndPoint(IPAddress.Any, settings.Port));
        }
        catch (SocketException ex)
        {
            AnsiConsole.MarkupLine($"[red]Cannot bind port {settings.Port}:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Another app is probably already listening (close other overlays/SimHub), or the port is reserved.[/]");
            return 1;
        }

        int packets = 0, valid = 0;
        var remote = new IPEndPoint(IPAddress.Any, 0);
        client.Client.ReceiveTimeout = 400;
        var sw = Stopwatch.StartNew();
        using (client)
        {
            while (sw.Elapsed.TotalSeconds < settings.Seconds && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var data = client.Receive(ref remote);
                    packets++;
                    if (data.Length == PacketParser.PacketSize) valid++;
                }
                catch (SocketException) { /* timeout tick; keep sampling */ }
            }
        }

        double pps = packets / Math.Max(0.001, sw.Elapsed.TotalSeconds);

        if (packets == 0)
        {
            AnsiConsole.MarkupLine("[red]No packets received.[/]");
            AnsiConsole.MarkupLine("Checklist:");
            AnsiConsole.MarkupLine("  • FH6 → Settings → HUD and Gameplay → Data Out = [bold]ON[/]");
            AnsiConsole.MarkupLine($"  • Game Data Out IP = [bold]127.0.0.1[/] (same PC), overlay listen = [bold]0.0.0.0[/], port = [bold]{settings.Port}[/]");
            AnsiConsole.MarkupLine("  • Data is only sent while [bold]actively driving[/] (not menus/pause/replay)");
            AnsiConsole.MarkupLine("  • No other app is bound to the port");
            return 2;
        }

        AnsiConsole.MarkupLine($"[green]Receiving data:[/] {packets} packets in {sw.Elapsed.TotalSeconds:F1}s (~{pps:F0}/s)");
        if (valid == packets)
            AnsiConsole.MarkupLine($"[green]All packets are valid FH6 frames[/] ({PacketParser.PacketSize} bytes).");
        else
            AnsiConsole.MarkupLine($"[yellow]{valid}/{packets} packets were {PacketParser.PacketSize} bytes[/] — wrong size suggests a different game/format on this port.");

        return 0;
    }
}
