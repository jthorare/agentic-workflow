using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The write-then-approve flow the page and pin sheets share (concept pins spec §3.6): the job's hash is approved only when the live profile is still that hash and nothing but the caller's paths waits.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileWriteFlowTests
{
    private const string JobId = "JOB-00000000000000000000000000000007";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static bool Mine(string path) => path.EndsWith(".pages.schedule-week", StringComparison.Ordinal);

    private static ProfileWriteFlow Flow(FakeSindriApi api, TimeProvider? clock = null) => new(api, () => new Uri("http://127.0.0.1:1/"), clock ?? new FakeTimeProvider(A.Noon));

    private static Task<ProfileWritten> Run(ProfileWriteFlow flow, FakeSindriApi api, bool approve = true) =>
        flow.Run((url, token) => api.SetContextPages(url, new PagesSetBody(IfHash: "h0"), token), Mine, approve, Token);

    private static RouteAnswer Profile(string live, params string[] changed) =>
        new(HttpStatusCode.OK, $$"""{"approved":"h0","live":"{{live}}","changed":true,"changedPaths":[{{string.Join(',', changed.Select(c => $"\"{c}\""))}}]}""");

    private static FakeSindriApi Server(RouteAnswer profile, string state = "succeeded", string? code = null, string? summary = null, string hash = "h1")
    {
        var api = new FakeSindriApi
        {
            ProfileAnswer = profile,
            SetContextPagesAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":12},"jobId":"{{JobId}}"}"""),
        };
        api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job(state, code, summary, hash));
        return api;
    }

    private static string Job(string state, string? code, string? summary, string hash) =>
        $$"""{"id":"{{JobId}}","kind":"SetPages","key":"k","item":null,"repo":null,"service":null,"state":"{{state}}","startedBy":"human","startedAt":"2026-10-11T12:00:00+00:00","endedAt":null,"progress":[],"rounds":0,"summary":{{Quoted(summary)}},"code":{{Quoted(code)}},"result":{"pages":[],"hash":"{{hash}}","written":true,"approved":false,"diff":[]},"cancelledBy":null,"actions":[],"cli":null}""";

    private static string Quoted(string? text) => text is null ? "null" : $"\"{text}\"";

    [Fact]
    public async Task Approves_when_only_these_paths_wait()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week"));

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Approved, written.State);
        Assert.Equal("h0", Assert.Single(api.PagesSet).IfHash);
    }

    [Fact]
    public async Task Approves_the_hash_the_write_job_returned()
    {
        var api = Server(Profile("h7", "index.pages.schedule-week"), hash: "h7");

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Approved, written.State);
        Assert.Equal("h7", Assert.Single(api.ProfileApprovals).Hash);
    }

    [Fact]
    public async Task Leaves_it_waiting_when_another_change_waits()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week", "index.concepts.approval"));

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Waiting, written.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Leaves_it_waiting_when_the_live_hash_moved_past_the_jobs()
    {
        var api = Server(Profile("h9", "index.pages.schedule-week"), hash: "h1");

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Waiting, written.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task A_stale_approve_leaves_it_waiting()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week"));
        api.ApproveProfileAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "The profile moved.");

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Waiting, written.State);
    }

    [Fact]
    public async Task Without_approve_it_never_approves()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week"));

        var written = await Run(Flow(api), api, approve: false);

        Assert.Equal(ProfileWriteState.Waiting, written.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task A_job_result_with_no_hash_leaves_it_waiting()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week"));
        api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("succeeded", code: null, summary: null, hash: "h1").Replace("\"hash\":\"h1\",", string.Empty, StringComparison.Ordinal));

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Waiting, written.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Reports_a_clash_and_reads_no_job()
    {
        var api = Server(Profile("h1"));
        api.SetContextPagesAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "The profile moved.");

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Clash, written.State);
        Assert.Equal(0, api.JobCalls);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Reports_a_refusal_with_the_servers_sentence()
    {
        var api = Server(Profile("h1"));
        const string Sentence = "index.pages[0].actions[0].concepts[0]: 'aproval' is no concept in map v41";
        api.SetContextPagesAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-CONTEXT-252", Sentence);

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Refused, written.State);
        Assert.Equal("SND-CONTEXT-252", written.Code);
        Assert.Equal(Sentence, written.Message);
    }

    [Fact]
    public async Task A_failed_job_is_a_refusal_when_its_code_is_a_context_one()
    {
        var api = Server(Profile("h1"), state: "failed", code: "SND-CONTEXT-174", summary: "no such concept");

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Refused, written.State);
        Assert.Equal("no such concept", written.Message);
    }

    [Fact]
    public async Task Fails_on_any_other_error()
    {
        var api = Server(Profile("h1"), state: "failed", code: "SND-JOB-500", summary: "boom");

        var written = await Run(Flow(api), api);

        Assert.Equal(ProfileWriteState.Failed, written.State);
        Assert.Equal("SND-JOB-500", written.Code);
    }

    [Fact]
    public async Task A_job_that_runs_past_30_seconds_fails()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var api = Server(Profile("h1", "index.pages.schedule-week"), state: "running");
        var run = Run(Flow(api, clock), api);

        while (!run.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        var written = await run;
        Assert.Equal(ProfileWriteState.Failed, written.State);
        Assert.Empty(api.ProfileApprovals);
        _ = Assert.Single(api.PagesSet);
    }

    [Fact]
    public async Task Sends_the_write_once_and_polls_while_the_job_runs()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var api = Server(Profile("h1", "index.pages.schedule-week"), state: "running");
        var run = Run(Flow(api, clock), api, approve: false);

        while (api.JobCalls < 2)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("succeeded", code: null, summary: null, hash: "h1"));
        while (!run.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        Assert.Equal(ProfileWriteState.Waiting, (await run).State);
        _ = Assert.Single(api.PagesSet);
    }

    [Fact]
    public async Task Keeps_polling_a_job_parked_on_a_signal()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var api = Server(Profile("h1", "index.pages.schedule-week"), state: "waiting");
        var run = Run(Flow(api, clock), api, approve: false);

        while (api.JobCalls < 2)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        Assert.False(run.IsCompleted);
        api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("succeeded", code: null, summary: null, hash: "h1"));
        while (!run.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        Assert.Equal(ProfileWriteState.Waiting, (await run).State);
    }

    [Fact]
    public async Task Waiting_reads_the_live_hash_and_changed_paths()
    {
        var waiting = await Flow(new FakeSindriApi { ProfileAnswer = Profile("h1", "index.pages.x") }).Waiting(Token);

        Assert.NotNull(waiting);
        Assert.Equal("h1", waiting.LiveHash);
        Assert.Equal(["index.pages.x"], waiting.ChangedPaths);
    }

    [Theory]
    [InlineData("index.pages.a", true)]
    [InlineData("index.concepts.a", false)]
    public void OnlyMine_holds_when_every_waiting_path_matches(string path, bool only) =>
        Assert.Equal(only, new ProfileWaiting("h1", [path]).OnlyMine(p => p.Contains(".pages.", StringComparison.Ordinal)));
}
