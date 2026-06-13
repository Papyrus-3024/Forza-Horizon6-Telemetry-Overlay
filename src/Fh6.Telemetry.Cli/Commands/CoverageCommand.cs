using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class CoverageCommand : Command<CoverageCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        public string File { get; init; } = "";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default) => 0;
}
