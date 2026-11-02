using System.Net;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>'context pin set' without --detach (concept pins spec §5): a pin refusal is invalid input, exit 2, whether the route refuses at the start or the followed job fails with it; any other failed job stays exit 1.</summary>
public sealed class ContextPinExitTests : IDisposable
{
    private const string Route = "/sindri/context/pins";
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private async Task<int> Run(JobApi api, params string[] args)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var verbs = new ContextPinVerbs(Client(api), _rig.Output(), _rig.Follower(api), services);
        return await verbs.Run(args, _rig.Interrupted, _rig.Again);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-173")]
    [InlineData("SND-CONTEXT-175")]
    public async Task A_refusal_at_the_start_exits_2(string code)
    {
        var api = new JobApi().On("POST", Route, Refused(HttpStatusCode.BadRequest, code, "refused"));

        var exit = await Run(api, "pin", "set", "Billing");

        Assert.Equal(2, exit);
        Assert.Contains(code, _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-172", 2)]
    [InlineData("SND-PROFILE-003", 2)]
    [InlineData("SND-GIT-001", 1)]
    public async Task A_followed_job_that_fails_with_a_pin_refusal_exits_2_and_any_other_failure_exits_1(string code, int expected)
    {
        var api = new JobApi().Ok("POST", Route, Body(Started)).Ok("GET", "/sindri/jobs/" + Id, View(JobState.Failed, rounds: 0, summary: "refused", code: code));
        var run = Task.Run(() => Run(api, "pin", "set", "Billing"), TestContext.Current.CancellationToken);
        await api.Until(() => api.Streams == 1);

        await api.Frame(12, "job.finished", Finished("failed", "refused", code));

        Assert.Equal(expected, await Finished(run));
    }
}
