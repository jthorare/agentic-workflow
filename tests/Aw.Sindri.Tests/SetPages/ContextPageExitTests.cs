using System.Net;
using Aw.Context.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.SetPages;

/// <summary>'context page set' without --detach (pages spec §3.6): an attention code is exit 1, a page refusal or a moved profile is invalid input, exit 2, whether the route refuses at the start or the followed job fails with it.</summary>
public sealed class ContextPageExitTests : IDisposable
{
    private const string Route = "/sindri/context/pages";
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private async Task<int> Run(JobApi api, params string[] args)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var verbs = new ContextPageWriteVerbs(Client(api), _rig.Output(), _rig.Follower(api), services);
        return await verbs.Run(args, _rig.Interrupted, _rig.Again);
    }

    // An attention code exits 1 wherever it is found; the others the page rules give exit 2.
    private static int Expected(string code) => ContextErrors.Attention.Contains(code) ? 1 : 2;

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-250")]
    [InlineData("SND-CONTEXT-251")]
    [InlineData("SND-CONTEXT-252")]
    [InlineData("SND-CONTEXT-254")]
    [InlineData("SND-CONTEXT-256")]
    [InlineData("SND-CONTEXT-257")]
    public async Task A_refusal_at_the_start_exits_by_the_attention_set(string code)
    {
        var api = new JobApi().On("POST", Route, Refused(HttpStatusCode.BadRequest, code, "refused"));

        var exit = await Run(api, "page", "set", "Week");

        Assert.Equal(Expected(code), exit);
        Assert.Contains(code, _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-252", 2)]
    [InlineData("SND-CONTEXT-254", 2)]
    [InlineData("SND-CONTEXT-257", 1)]
    [InlineData("SND-PROFILE-003", 2)]
    [InlineData("SND-GIT-001", 1)]
    public async Task A_followed_job_that_fails_with_a_page_refusal_exits_2_attention_and_other_failures_exit_1(string code, int expected)
    {
        var api = new JobApi().Ok("POST", Route, Body(Started)).Ok("GET", "/sindri/jobs/" + Id, View(JobState.Failed, rounds: 0, summary: "refused", code: code));
        var run = Task.Run(() => Run(api, "page", "set", "Week"), TestContext.Current.CancellationToken);
        await api.Until(() => api.Streams == 1);

        await api.Frame(12, "job.finished", Finished("failed", "refused", code));

        Assert.Equal(expected, await Finished(run));
    }
}
