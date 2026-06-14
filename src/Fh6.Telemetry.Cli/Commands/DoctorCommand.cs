using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

/// <summary>
/// Connection diagnostics: binds the Data Out port, samples for a few seconds, reports
/// packets/sec and packet validity, and prints the UWP/Store loopback fix — the single
/// most-cited Forza-PC setup pain (see qol-research.md #1/#2).
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
            PrintLoopbackHelp();
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
            AnsiConsole.MarkupLine($"  • Data Out IP points at THIS pc ([bold]127.0.0.1[/] if same machine), port = [bold]{settings.Port}[/]");
            AnsiConsole.MarkupLine("  • Data is only sent while [bold]actively driving[/] (not menus/pause/replay)");
            PrintLoopbackHelp();
            return 2;
        }

        AnsiConsole.MarkupLine($"[green]Receiving data:[/] {packets} packets in {sw.Elapsed.TotalSeconds:F1}s (~{pps:F0}/s)");
        if (valid == packets)
            AnsiConsole.MarkupLine($"[green]All packets are valid FH6 frames[/] ({PacketParser.PacketSize} bytes).");
        else
            AnsiConsole.MarkupLine($"[yellow]{valid}/{packets} packets were {PacketParser.PacketSize} bytes[/] — wrong size suggests a different game/format on this port.");

        return 0;
    }

    private static void PrintLoopbackHelp()
    {
        AnsiConsole.MarkupLine("\n[bold]If FH6 is the Microsoft Store / Game Pass (UWP) build[/] and the game is on this same PC,");
        AnsiConsole.MarkupLine("UWP apps are sandboxed from localhost. Run this once in an [bold]admin[/] PowerShell:");
        AnsiConsole.MarkupLine("  [grey]CheckNetIsolation LoopbackExempt -a -n=\"Microsoft.624F8B84B80_8wekyb3d8bbwe\"[/]");
        AnsiConsole.MarkupLine("[grey](package name varies by title; `CheckNetIsolation LoopbackExempt -s` lists exemptions.)[/]");
    }
}
