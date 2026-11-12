using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RestartServe;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.RestartServe;

/// <summary>The runs a restart keeps are the sessions whose launch has an instance, the filter scripts/lib/sindri-open-runs.sh uses.</summary>
public sealed class OpenRunsTests
{
    private readonly SessionsRig _r = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_launched_session_with_an_instance_is_an_open_run_and_a_session_without_one_is_not()
    {
        await _r.Launches.Item(Core.Ids.RepoName.From("api"));
        await _r.Launches.Start(instance: InstanceAddress.From("T-3/item/1"), state: "build", attempt: 1);
        _ = _r.Publish(SessionsRig.Three);

        var runs = await new OpenRuns(_r.Snapshot, _r.LaunchViews).Read(Token);

        var run = Assert.Single(runs);
        Assert.Equal(new KeptRun($"{SessionsRig.Cursor().Ref}", "T-3/item/1", "build"), run);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launch_without_an_instance_is_not_an_open_run()
    {
        await _r.Launches.Item(Core.Ids.RepoName.From("api"));
        await _r.Launches.Start();
        _ = _r.Publish(SessionsRig.Three);

        Assert.Empty(await new OpenRuns(_r.Snapshot, _r.LaunchViews).Read(Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_the_collectors_first_pass_there_are_no_open_runs()
    {
        Assert.Empty(await new OpenRuns(_r.Snapshot, _r.LaunchViews).Read(Token));
    }
}
