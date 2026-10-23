using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>The T3 host against the hosted-sessions contract: a thread the user archives is gone.</summary>
[Lane(TestLane.Integration)]
public sealed class T3HostedSessionsContractTests : HostedSessionsContract, IDisposable
{
    private readonly T3Rig _rig = new();

    public void Dispose() => _rig.Dispose();

    protected override IHostedSessions Create() => _rig.Host();

    protected override async Task Start(IHostedSessions sessions, LaunchId launch) =>
        _ = await ((T3SessionHost)sessions).Start(_rig.Session(LaunchSessionName.For(launch)), TimeSpan.FromSeconds(4), CancellationToken.None);

    protected override void EndOutside(IHostedSessions sessions, LaunchId launch) => _rig.Api.Archive(_rig.Api.Launched.Count == 0 ? "none" : $"thread-{_rig.Api.Launched.Count}");
}
