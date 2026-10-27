using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class ScriptedCheckRunnerContractTests : CheckRunnerContract
{
    protected override ICheckRunner Runner { get; } = new ScriptedCheckRunner().Killed("killed").Silent("quiet");

    protected override string Directory => "/repo";

    protected override ICheckRunner HangingRunner { get; } = new ScriptedCheckRunner().Hangs("sleep");

    protected override IReadOnlyList<string> HangingCommand => ["sleep", "5"];

    protected override IReadOnlyList<string> PassingCommand => ["true"];

    protected override IReadOnlyList<string> FailingCommand => ["false"];

    protected override IReadOnlyList<string> KilledCommand => ["killed"];

    protected override IReadOnlyList<string> SilentCommand => ["quiet"];
}
