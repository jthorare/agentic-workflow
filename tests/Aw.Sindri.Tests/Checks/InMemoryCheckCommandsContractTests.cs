using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Testing.Checks;

namespace Aw.Sindri.Tests.Checks;

[Lane(TestLane.Unit)]
public sealed class InMemoryCheckCommandsContractTests : CheckCommandsContract
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(300);

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Prints(int exitCode, string output) =>
        (new InMemoryCheckCommands().Answer("c", CheckRun.Of(exitCode, timedOut: false, TimeSpan.Zero, output)), new CheckInvocation("c", ["c"], _timeout), "/work");

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Hangs() =>
        (new InMemoryCheckCommands().Answer("c", CheckRun.Of(-1, timedOut: true, _timeout, string.Empty)), new CheckInvocation("c", ["c"], _timeout), "/work");

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Missing() =>
        (new InMemoryCheckCommands(), new CheckInvocation("c", ["no-such-tool"], _timeout), "/work");
}
