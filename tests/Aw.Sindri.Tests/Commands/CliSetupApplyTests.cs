using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri setup --apply': adds the repos, runs the setup pass and waits for it, then prints what it did and the steps (first-run spec decision 10, §5.1).</summary>
public sealed class CliSetupApplyTests : IDisposable
{
    private const string Repos = "/sindri/setup/repos";
    private const string Pass = "/sindri/services/setup/run";
    private const string Job = "/sindri/jobs/" + JobRig.Id;
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static string Setup() => JobRig.Body(new GetSetupResponse([SetupSteps.Repo(new RepoFact(1))], new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));

    private static JobApi Api() => new JobApi().Ok("GET", "/sindri/setup", Setup()).Ok("POST", Repos, JobRig.Body(JobRig.Started)).Ok("POST", Pass, JobRig.Body(JobRig.Started))
        .Ok("GET", Job, JobRig.View(JobState.Succeeded, rounds: 0, "done"));

    private Task<int> Run(JobApi api, params string[] args) =>
        Task.Run(() => new SetupApplyVerb(JobRig.Client(api), _rig.Output(), (_, _) => Task.FromResult<Kernel.Error?>(null), _rig.Clock).Run(args, TestContext.Current.CancellationToken));

    private string Out => _rig.Out.ToString().ReplaceLineEndings("\n");

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_adds_each_named_repo_runs_the_setup_pass_and_exits_0()
    {
        var api = Api();

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo"));

        Assert.Equal(0, code);
        Assert.Equal(1, api.Count("POST " + Repos));
        Assert.Equal(1, api.Count("POST " + Pass));
        Assert.Contains("added     /work/demo\n", Out, StringComparison.Ordinal);
        Assert.Contains("setup pass done in 0 s (" + JobRig.Id + ")\n", Out, StringComparison.Ordinal);
        Assert.Contains("1 of 1 steps done.\n", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_counts_a_repo_the_profile_names_already_as_already_and_exits_0()
    {
        var api = Api().On("POST", Repos, JobRig.Refused(HttpStatusCode.Conflict, "SND-SETUP-002", "that folder is already the repo demo (fix: pick another folder)"));

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo"));

        Assert.Equal(0, code);
        Assert.Contains("already   /work/demo\n", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_exits_1_and_names_a_repo_it_could_not_add()
    {
        var api = Api().On("POST", Repos, JobRig.Refused(HttpStatusCode.BadRequest, "SND-SETUP-001", "that folder isn't the top of a git repo"));

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo"));

        Assert.Equal(1, code);
        Assert.Contains("failed    /work/demo: SND-SETUP-001 ", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_with_no_repo_named_adds_the_known_repos_a_scan_finds_and_prints_each()
    {
        var api = Api().Ok("GET", Repos, Scan(("/work/demo", true, false), ("/work/old", true, true), ("/work/other", false, false)));

        var code = await JobRig.Finished(Run(api, "--apply"));

        Assert.Equal(0, code);
        Assert.Equal(1, api.Count("POST " + Repos));
        Assert.Contains("added     /work/demo\nalready   /work/old\n", Out, StringComparison.Ordinal);
        Assert.DoesNotContain("/work/other", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_with_no_repo_named_and_none_known_refuses_with_SND_SETUP_010()
    {
        var api = Api().Ok("GET", Repos, Scan(("/work/other", false, false)));

        var code = await JobRig.Finished(Run(api, "--apply"));

        Assert.Equal((2, 0, 0), (code, api.Count("POST " + Repos), api.Count("POST " + Pass)));
        Assert.StartsWith("SND-SETUP-010 ", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_reports_a_pass_that_did_not_finish_in_time_with_SND_SETUP_015_and_exits_1()
    {
        var api = Api().Ok("GET", Job, JobRig.View(JobState.Succeeded, rounds: 0, "done"), JobRig.View(JobState.Running, rounds: 0));   // the repo's job ends; the pass's keeps running
        var run = Run(api, "--apply", "--repo", "/work/demo", "--timeout", "2s");

        await _rig.AdvanceOnTimers(TimeSpan.FromSeconds(1), run);

        Assert.Equal(1, await run);
        Assert.Contains($"SND-SETUP-015 the setup pass didn't finish in 2 s; it keeps running as {JobRig.Id} (fix: sindri jobs show {JobRig.Id}, then sindri setup)", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_reports_a_failed_pass_with_SND_SETUP_016_and_exits_1()
    {
        var api = Api().Ok("GET", Job, JobRig.View(JobState.Succeeded, rounds: 0, "done"), JobRig.View(JobState.Failed, rounds: 0, "the agent target couldn't be set\nmore", "SND-SETUP-007"));

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo"));

        Assert.Equal(1, code);
        Assert.Contains($"SND-SETUP-016 the setup pass failed: the agent target couldn't be set (fix: sindri jobs show {JobRig.Id})", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_follows_a_setup_pass_that_runs_already()
    {
        var api = Api().On("POST", Pass, JobRig.Refused(HttpStatusCode.Conflict, "SND-JOB-409", $"A RunService job is already running for setup (fix: run `sindri jobs show {JobRig.Id}`, or wait for it to end)"));

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo"));

        Assert.Equal(0, code);
        Assert.Contains("setup pass done in 0 s (" + JobRig.Id + ")\n", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_with_timeout_0_starts_the_pass_and_does_not_wait()
    {
        var api = Api().Ok("GET", Job, JobRig.View(JobState.Succeeded, rounds: 0, "done"), JobRig.View(JobState.Running, rounds: 0));

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo", "--timeout", "0"));

        Assert.Equal((0, 1), (code, api.Count("GET " + Job)));
        Assert.Contains("not waiting for it (--timeout 0)", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_says_refused_when_this_caller_may_not_run_services_and_exits_0()
    {
        var api = Api().On("POST", Pass, JobRig.Refused(HttpStatusCode.Forbidden, "SND-AUTH-002", "This caller may not run the command"));

        var code = await JobRig.Finished(Run(api, "--apply", "--repo", "/work/demo", "--json"));

        Assert.Equal(0, code);
        using var document = JsonDocument.Parse(_rig.Out.ToString());
        Assert.Equal("refused", document.RootElement.GetProperty("setupPass").GetProperty("state").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_apply_json_prints_one_document_with_the_repos_the_pass_and_the_steps()
    {
        var code = await JobRig.Finished(Run(Api(), "--apply", "--repo", "/work/demo", "--json"));

        Assert.Equal(0, code);
        using var document = JsonDocument.Parse(_rig.Out.ToString());
        var root = document.RootElement;
        string[] keys = ["added", "already", "failed", "serveStarted", "setupPass", "steps"];
        string[] passKeys = ["state", "jobId", "seconds"];
        Assert.Equal(keys, root.EnumerateObject().Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Equal("/work/demo", root.GetProperty("added")[0].GetString());
        Assert.False(root.GetProperty("serveStarted").GetBoolean());
        Assert.Equal(passKeys, root.GetProperty("setupPass").EnumerateObject().Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Equal("done", root.GetProperty("setupPass").GetProperty("state").GetString());
        Assert.Equal("repo", root.GetProperty("steps")[0].GetProperty("id").GetString());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--apply", "--bogus")]
    [InlineData("--apply", "--timeout", "soon")]
    [InlineData("--apply", "--repo")]
    public async Task Setup_apply_refuses_a_line_it_does_not_know_before_calling_anything(params string[] args)
    {
        var api = Api();

        var code = await JobRig.Finished(Run(api, args));

        Assert.Equal((2, 0), (code, api.Calls.Count));
    }

    // NDJSON as GET /sindri/setup/repos streams it: one row per checkout, then the end line.
    private static string Scan(params (string Path, bool Known, bool InProfile)[] rows) =>
        string.Concat(rows.Select(static r => $$"""{"path":"{{r.Path}}","host":"github.com","slug":"x/y","known":{{(r.Known ? "true" : "false")}},"inProfile":{{(r.InProfile ? "true" : "false")}},"worktrees":0}""" + "\n"))
        + $$"""{"done":true,"folders":3,"repos":{{rows.Length}},"knownFailed":false}""" + "\n";
}
