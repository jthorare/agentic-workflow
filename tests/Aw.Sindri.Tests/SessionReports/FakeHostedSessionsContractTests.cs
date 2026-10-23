using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The fake hosted sessions against their contract.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeHostedSessionsContractTests : HostedSessionsContract
{
    protected override IHostedSessions Create() => new FakeHostedSessions();

    protected override Task Start(IHostedSessions sessions, LaunchId launch)
    {
        ((FakeHostedSessions)sessions).Add(launch);
        return Task.CompletedTask;
    }

    protected override void EndOutside(IHostedSessions sessions, LaunchId launch) => ((FakeHostedSessions)sessions).MarkGone(launch);
}
