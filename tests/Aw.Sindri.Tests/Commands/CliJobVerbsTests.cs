using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListJobs;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri jobs', 'jobs show' and 'jobs cancel' (API P6 unit H): clients of the job routes.</summary>
public sealed class CliJobVerbsTests : IDisposable
{
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_list_sends_the_filters_and_prints_a_line_each()
    {
        var list = Body(new JobList([new JobSummary(JobId.From(Id), JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, ServiceName.From("heartbeat"),
            JobState.Failed, "human:local", KnownInstants.Epoch, KnownInstants.Epoch, Rounds: 4, "the service threw", "SND-SCHED-001")], Empty: null,
            new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));
        var api = new JobApi().Ok("GET", "/sindri/jobs", list);

        var code = await Finished(_rig.RunJobs(api, "--state", "failed", "--kind", "RunService"));

        Assert.Equal(0, code);
        Assert.Equal(1, api.Count("GET /sindri/jobs?state=failed&kind=RunService"));
        Assert.Equal($"{Id}  failed  RunService  heartbeat  2026-01-01T00:00:00Z  4 lines  the service threw SND-SCHED-001\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_list_says_1_line_for_a_single_line()
    {
        var list = Body(new JobList([new JobSummary(JobId.From(Id), JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, ServiceName.From("heartbeat"),
            JobState.Succeeded, "human:local", KnownInstants.Epoch, KnownInstants.Epoch, Rounds: 1, "ok: worked", Code: null)], Empty: null,
            new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));
        var api = new JobApi().Ok("GET", "/sindri/jobs", list);

        _ = await Finished(_rig.RunJobs(api));

        Assert.Equal($"{Id}  succeeded  RunService  heartbeat  2026-01-01T00:00:00Z  1 line  ok: worked\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_show_prints_the_job_and_its_lines()
    {
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, View(JobState.Running, rounds: 2));

        var code = await Finished(_rig.RunJobs(api, "show", Id));

        Assert.Equal(0, code);
        Assert.Equal($"{Id}  running  RunService  heartbeat\nstarted by human:local at 2026-01-01T00:00:00Z\nline 1\nline 2\ncancel: sindri jobs cancel {Id}\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_show_json_is_the_route_body()
    {
        var body = View(JobState.Succeeded, rounds: 1, summary: "ok");
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, body);

        var code = await Finished(_rig.RunJobs(api, "show", Id, "--json"));

        Assert.Equal((0, body + "\n"), (code, _rig.Out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_cancel_prints_the_sequence()
    {
        var api = new JobApi().Ok("POST", "/sindri/jobs/" + Id + "/cancel", Body(new Core.Commands.CommandAccepted(CommandId.From(new Guid(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 12)));

        var code = await Finished(_rig.RunJobs(api, "cancel", Id));

        Assert.Equal((0, $"{Id} cancel requested (sequence 12)\n"), (code, _rig.Out.ToString().ReplaceLineEndings("\n")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("show")]
    [InlineData("cancel")]
    [InlineData("--wat")]
    [InlineData("show", "a", "b")]
    public async Task A_bad_command_line_is_refused_without_calling(params string[] args)
    {
        var api = new JobApi();

        var code = await Finished(_rig.RunJobs(api, args));

        Assert.Equal(2, code);
        Assert.Empty(api.Calls);
    }

    private static readonly DateTimeOffset _noon = new(2026, 1, 1, 14, 2, 11, TimeSpan.Zero);

    private static JobModelCall Call(string id, DateTimeOffset at, string kind, int input, int output, JobModelAnswerer? answered, JobModelHow? how = null, int? items = null, params JobModelTry[] tries) =>
        new(ModelCallId.From(id), kind, kind, tries, answered, input, output, ModelTokenBasis.Reported, items, how, at, Ms: 1500);

    private static JobModelTry Try(string harness, string model, string outcome, string? reason, int ms) => new(harness, model, outcome, reason, ms, "builtin");

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_show_prints_a_Model_calls_block_with_a_row_per_try()
    {
        _rig.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero));
        var calls = new[]
        {
            Call("c1", _noon, "labeling", 3000, 1100, new JobModelAnswerer("codex", "gpt-6-luna"),
                tries: [Try("claude-code", "haiku", "limit", "limit", 1000), Try("codex", "gpt-6-luna", "answered", reason: null, 1200)]),
            Call("c2", _noon.AddSeconds(29), "labeling", 3000, 800, new JobModelAnswerer("codex", "gpt-6-luna"),
                tries: [Try("claude-code", "haiku", "skipped", "limited", 0), Try("codex", "gpt-6-luna", "answered", reason: null, 1200)]),
            Call("c3", _noon.AddSeconds(39), "labeling", 0, 0, new JobModelAnswerer("jev", "jev-1.13.0"), new JobModelHow(Jev: 94, FirstCandidate: 5, FileName: 1), items: 100,
                tries: [Try("jev", "jev-1.13.0", "answered", reason: null, 800)]),
        };
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, ModelView(JobState.Running, waitingOn: null, ["building"], calls, count: 3));

        var code = await Finished(_rig.RunJobs(api, "show", Id));

        Assert.Equal(0, code);
        Assert.Equal($"{Id}  running  BuildContextMap  map\nstarted by human:local at 2026-01-01T00:00:00Z\nbuilding\n" +
            "Model calls\n" +
            "14:02:11  labeling       claude-code haiku        limit\n" +
            "14:02:12  labeling       codex gpt-6-luna         answered   4.1k tokens\n" +
            "14:02:40  labeling       claude-code haiku        skipped    limited earlier in this job\n" +
            "14:02:40  labeling       codex gpt-6-luna         answered   3.8k tokens\n" +
            "14:02:50  labeling       jev jev-1.13.0           answered   picked 94 of 100, 5 first, 1 file name\n" +
            $"cancel: sindri jobs cancel {Id}\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_job_parked_on_model_limit_starts_with_why_and_how_to_resume()
    {
        _rig.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero));
        var line = "Paused: claude-code at its usage limit; codex at its usage limit. Resume when one is back.";
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, ModelView(JobState.Waiting, "model-limit", ["building", line], [], count: 0));

        _ = await Finished(_rig.RunJobs(api, "show", Id));

        Assert.StartsWith($"waiting on model-limit: claude-code at its usage limit; codex at its usage limit. Resume with: sindri jobs resume {Id}\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_try_that_failed_names_its_code_and_a_skipped_pick_says_why_it_was_not_asked()
    {
        _rig.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero));
        var calls = new[]
        {
            Call("c1", _noon, "labeling", 0, 0, answered: null, tries: [Try("claude-code", "haiku", "failed", "exit-1", 400)]),
            Call("c2", _noon.AddSeconds(5), "labeling", 0, 0, answered: null, tries: [Try("jev", "jev-1.13.0", "not-asked", "no-key", 0)]),
        };
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, ModelView(JobState.Running, waitingOn: null, [], calls, count: 2));

        _ = await Finished(_rig.RunJobs(api, "show", Id));

        var text = _rig.Out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("14:02:11  labeling       claude-code haiku        failed     exit-1\n", text, StringComparison.Ordinal);
        Assert.Contains("14:02:16  labeling       jev jev-1.13.0           not asked  no key\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task More_calls_than_the_job_keeps_say_how_many_are_shown_and_a_row_not_from_today_carries_its_date()
    {
        _rig.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));
        var calls = new[] { Call("c1", _noon, "labeling", 100, 50, new JobModelAnswerer("codex", "gpt-6-luna"), tries: [Try("codex", "gpt-6-luna", "answered", reason: null, 900)]) };
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, ModelView(JobState.Running, waitingOn: null, [], calls, count: 205));

        _ = await Finished(_rig.RunJobs(api, "show", Id));

        var text = _rig.Out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("Model calls\nshowing the last 1 of 205\n2026-01-01 14:02:11  labeling       codex gpt-6-luna         answered   150 tokens\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_show_json_carries_models_waitingOn_and_modelCalls()
    {
        var body = ModelView(JobState.Waiting, "model-limit", ["Paused: x. Resume when one is back."], [Call("c1", _noon, "labeling", 1, 1, new JobModelAnswerer("codex", "gpt-6-luna"), tries: [Try("codex", "gpt-6-luna", "answered", reason: null, 1)])], count: 1);
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, body);

        _ = await Finished(_rig.RunJobs(api, "show", Id, "--json"));

        var json = System.Text.Json.JsonDocument.Parse(_rig.Out.ToString()).RootElement;
        Assert.Equal("model-limit", json.GetProperty("waitingOn").GetString());
        Assert.Equal(1, json.GetProperty("models").GetProperty("calls").GetInt32());
        Assert.Equal("c1", json.GetProperty("modelCalls")[0].GetProperty("callId").GetString());
    }
}
