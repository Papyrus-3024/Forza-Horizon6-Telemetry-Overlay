using Fh6.Telemetry.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("fh6");
    config.AddCommand<CaptureCommand>("capture")
        .WithDescription("Record live UDP telemetry to a JSONL capture file.");
    config.AddCommand<ReplayCommand>("replay")
        .WithDescription("Replay a capture file to the dashboard.");
    config.AddCommand<LiveCommand>("live")
        .WithDescription("Show the live telemetry dashboard from UDP.");
});
return app.Run(args);
