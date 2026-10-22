using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Infrastructure.Checks;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Checks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Checks;

/// <summary>The adapter over a scripted tool runner honours the check-commands contract.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolCheckCommandsContractTests : CheckCommandsContract
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(300);

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Prints(int exitCode, string output) =>
        (Over(new ToolRun(ToolOutcome.Exited, exitCode, output)), new CheckInvocation("c", ["c"], _timeout), "/work");

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Hangs() =>
        (Over(ToolRun.TimedOut), new CheckInvocation("c", ["c"], _timeout), "/work");

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Missing() =>
        (Over(ToolRun.Missing), new CheckInvocation("c", ["no-such-tool"], _timeout), "/work");

    private static ToolCheckCommands Over(ToolRun answer) => new(new FakeToolRunner(answer), new IdentityScrubber(), new FakeTimeProvider());
}
