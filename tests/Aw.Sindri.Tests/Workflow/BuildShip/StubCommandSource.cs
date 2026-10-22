using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>The repo's commands, as the test lists them: command lines the way the profile writes them.</summary>
/// <param name="lines">The command lines.</param>
/// <param name="each">The longest one may run.</param>
internal sealed class StubCommandSource(string[] lines, TimeSpan each) : IBuildCommandSource
{
    internal static CommandSet Set(TimeSpan each, params string[] lines) =>
        new([.. lines.Select(static line => CommandLine.TryParse(line, out var argv, out _) ? new BuildCommand(line, argv) : throw new ArgumentException($"'{line}' does not split", nameof(lines)))], each);

    public ValueTask<Result<CommandSet>> Read(CancellationToken cancellationToken) => ValueTask.FromResult<Result<CommandSet>>(Set(each, lines));
}
