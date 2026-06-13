using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class ReplayCommand : Command<ReplayCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to a JSONL capture file.")]
        public string File { get; init; } = "";

        [CommandOption("-s|--speed")]
        [Description("Playback speed multiplier (1.0 = realtime).")]
        [DefaultValue(1.0)]
        public double Speed { get; init; }

        [CommandOption("-l|--loop")]
        [Description("Loop the capture continuously.")]
        public bool Loop { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var source = new JsonlReplaySource(settings.File);
        var dashboard = new SpectreDashboard();

        dashboard.Run(render =>
        {
            do
            {
                double? prevT = null;
                var rendered = 0;
                foreach (var frame in source.Frames())
                {
                    if (prevT is double previous && settings.Speed > 0)
                    {
                        var waitMs = (frame.TimestampMs - previous) / settings.Speed;
                        if (waitMs > 0)
                            Thread.Sleep(TimeSpan.FromMilliseconds(waitMs));
                    }
                    prevT = frame.TimestampMs;

                    if (PacketParser.TryParse(frame.Data, out var packet))
                    {
                        render(packet);
                        rendered++;
                    }
                }

                // Nothing to play (empty or all-unparseable capture); don't spin on --loop.
                if (rendered == 0)
                    break;
            } while (settings.Loop);
        });

        return 0;
    }
}
