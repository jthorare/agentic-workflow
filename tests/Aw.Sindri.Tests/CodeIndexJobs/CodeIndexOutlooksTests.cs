using Aw.CodeIndex.Core.Values;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The Host-side half of the map publish gate (code index spec §6.4): how stored heads, live heads, the 'index:&lt;repo&gt;' jobs and the service state become an <see cref="IndexOutlookInput"/>.</summary>
public sealed class CodeIndexOutlooksTests
{
    private static readonly TimeSpan _minute = TimeSpan.FromMinutes(1);
    private readonly CodeIndexJobsRig _rig = new();
    private IServicePauses _pauses = new NoPauses();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MapHoldState Hold(string repo, CommitSha head, bool onDefault = true) =>
        new(Version: 3, CodeIndexJobsRig.Now - TimeSpan.FromMinutes(5), new Dictionary<string, MapRepoHead>(StringComparer.Ordinal) { [repo] = new MapRepoHead(head.Value, onDefault) });

    private async Task<IndexOutlookDetail> Read(MapHoldState hold)
    {
        await _rig.Approve(Ct);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);
        var outlooks = new CodeIndexOutlooks(_rig.Scopes, _rig.Index, _rig.Heads, _rig.Jobs, _pauses, _rig.Harness.Clock);
        var read = await outlooks.Read(hold, Ct);
        return read.Value[hold.Heads.Keys.Single()];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_at_the_holds_head_is_built()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New);

        Assert.Equal(IndexOutlook.Built, (await Read(Hold("demo", CodeIndexJobsRig.New))).Outlook);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_never_had_an_index_is_absent_and_worded_not_indexed()
    {
        var detail = await Read(Hold("demo", CodeIndexJobsRig.New));

        Assert.Equal((IndexOutlook.Absent, RepoWait.NotIndexed), (detail.Outlook, detail.Word));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_at_another_commit_with_nothing_running_is_coming_queued()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);

        var detail = await Read(Hold("demo", CodeIndexJobsRig.New));

        Assert.Equal((IndexOutlook.Coming, RepoWait.Queued), (detail.Outlook, detail.Word));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_job_of_the_repos_key_is_coming_building_and_another_repos_job_is_not_counted()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        _rig.Job("other", JobState.Running, TimeSpan.Zero);
        Assert.Equal(RepoWait.Queued, (await Read(Hold("demo", CodeIndexJobsRig.New))).Word);

        _rig.Job("demo", JobState.Running, TimeSpan.Zero);
        var detail = await Read(Hold("demo", CodeIndexJobsRig.New));

        Assert.Equal((IndexOutlook.Coming, RepoWait.Building), (detail.Outlook, detail.Word));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_job_is_backoff_retrying_until_its_retry_is_due()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        _rig.Job("demo", JobState.Failed, _minute);

        var detail = await Read(Hold("demo", CodeIndexJobsRig.New));

        Assert.Equal((IndexOutlook.Backoff, RepoWait.Retrying), (detail.Outlook, detail.Word));
        Assert.Equal(CodeIndexJobsRig.Now - _minute + SyncGapDecision.Wait(1), detail.RetryAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_failed_jobs_in_a_row_are_broken_and_a_success_between_resets_the_count()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        _rig.Job("demo", JobState.Failed, TimeSpan.FromHours(3));
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromHours(2), "not-on-main");
        _rig.Job("demo", JobState.Failed, TimeSpan.FromHours(1));
        _rig.Job("demo", JobState.Failed, TimeSpan.FromMinutes(30));
        Assert.NotEqual(IndexOutlook.Broken, (await Read(Hold("demo", CodeIndexJobsRig.New))).Outlook);

        _rig.Job("demo", JobState.Failed, _minute);

        Assert.Equal(IndexOutlook.Broken, (await Read(Hold("demo", CodeIndexJobsRig.New))).Outlook);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_published_job_with_no_readable_index_now_is_coming_damaged_not_never_built()
    {
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromHours(1), outcome: "published");

        var detail = await Read(Hold("demo", CodeIndexJobsRig.New));

        Assert.Equal((IndexOutlook.Coming, RepoWait.Damaged), (detail.Outlook, detail.Word));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paused_service_leaves_the_repo_absent()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        _pauses = new PausedServices(ServiceName.From("code-index-main-move"));

        Assert.Equal(IndexOutlook.Absent, (await Read(Hold("demo", CodeIndexJobsRig.New))).Outlook);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hold_head_that_was_not_on_the_default_branch_and_a_repo_outside_the_scope_are_absent()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        Assert.Equal(IndexOutlook.Absent, (await Read(Hold("demo", CodeIndexJobsRig.New, onDefault: false))).Outlook);

        Assert.Equal(IndexOutlook.Absent, (await Read(Hold("elsewhere", CodeIndexJobsRig.New))).Outlook);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task IndexCommits_names_the_stored_commit_of_each_asked_repo_that_has_one()
    {
        await _rig.Approve(Ct);
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        var outlooks = new CodeIndexOutlooks(_rig.Scopes, _rig.Index, _rig.Heads, _rig.Jobs, _pauses, _rig.Harness.Clock);

        var commits = (await outlooks.IndexCommits(["demo", "other"], Ct)).Value;

        Assert.Equal(("demo", CodeIndexJobsRig.Old.Value), (commits.Keys.Single(), commits.Values.Single()));
    }
}
