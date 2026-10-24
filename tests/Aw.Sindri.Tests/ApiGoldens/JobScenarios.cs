using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Eval.Backtest;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P6's goldens (plan §6): the job and service routes. Every scenario runs on <see cref="JobHost"/> (two repos, the scheduled 'heartbeat' and a
/// 'stuck' service whose runs wait): it approves the profile, then either starts the real thing through the API (a run) or appends the job's events
/// itself and drives Projector.CatchUp on the fake clock, so a state like 'interrupted' or a 250-line job needs no process to die. Ids are sequential.
/// </summary>
internal static class JobScenarios
{
    private static readonly string _unknownJob = "JOB-" + new string('0', 30) + "ff";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("services-name-run", "started"), ("services-name-run", "refused-session"), ("services-name-run", "refused-no-header"), ("services-name-run", "refused-origin"),
        ("services-name-run", "unknown-service"), ("services-name-run", "already-running"), ("services-name-run", "scheduled-in-flight"), ("services-name-run", "too-many"),
        ("services-name-pause", "accepted"), ("services-name-pause", "refused-session"), ("services-name-pause", "unknown-service"),
        ("services-name-resume", "accepted"), ("services-name-resume", "refused-session"), ("services-name-resume", "unknown-service"),
        ("services", "paused"), ("services", "running-job"), ("services", "manual-ok"), ("needs-you", "after-manual-run"), ("needs-you", "unknown-service"), ("events", "job-lifecycle"),
        ("jobs-id", "running"), ("jobs-id", "succeeded"), ("jobs-id", "failed"), ("jobs-id", "cancelled"), ("jobs-id", "interrupted"), ("jobs-id", "progress-capped"),
        ("jobs-id", "context-backtest"), ("jobs-id", "ansi-escaped"), ("jobs-id", "result"), ("jobs-id", "not-found"), ("jobs-id", "bad-id"), ("jobs-id", "session-other-repo"), ("jobs-id", "stalled"),
        ("jobs-id-cancel", "accepted"), ("jobs-id-cancel", "already-ended"), ("jobs-id-cancel", "not-found"), ("jobs-id-cancel", "refused-session"),
        ("jobs", "running"), ("jobs", "empty"), ("jobs", "by-kind"), ("jobs", "empty-unfiltered"), ("jobs", "bad-kind"), ("jobs", "bad-state"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> (started by <see cref="JobHost.Start"/>) and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        return slug switch
        {
            "services-name-run" => await Run(host, scenario),
            "services-name-pause" or "services-name-resume" => await PauseOrResume(host, slug[(slug.LastIndexOf('-') + 1)..], scenario),
            "services" when scenario == "manual-ok" => await AfterManualRun(host, "/sindri/services", ct),
            "needs-you" when scenario == "unknown-service" => await UnknownService(host, ct),
            "needs-you" => await AfterManualRun(host, "/sindri/needs-you", ct),
            "events" => await Lifecycle(host, ct),
            "services" => await Services(host, scenario, ct),
            "jobs-id" => await JobBody(host, scenario, ct),
            "jobs-id-cancel" => await Cancel(host, scenario, ct),
            "jobs" => await List(host, scenario, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "no such job slug"),
        };
    }

    private static (string Name, string Value)[] Session => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")];

    private static JobId Job(int n) => JobId.From($"JOB-{n.ToString("x32", CultureInfo.InvariantCulture)}");

    private static CommandId Command(int n) => CommandId.From(new Guid(0xc0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)n));

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;

    private static async Task<string> Run(GoldenHost host, string scenario)
    {
        var human = JobHost.Human(host);
        var path = "/sindri/services/heartbeat/run";
        return scenario switch
        {
            "started" => await Ask(host, Post(path, json: null, human)),
            "refused-session" => await Ask(host, Post(path, json: null, Session)),
            "refused-no-header" => await Ask(host, Post(path, json: null)),
            "refused-origin" => await Ask(host, Post(path, json: null, [.. human, ("Origin", "http://localhost:3000")])),
            "unknown-service" => await Ask(host, Post("/sindri/services/ghost/run", json: null, human)),
            "already-running" or "scheduled-in-flight" or "too-many" => await RunInFlight(host, scenario),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such run scenario"),
        };
    }

    private static async Task<string> RunInFlight(GoldenHost host, string scenario)
    {
        var human = JobHost.Human(host);
        switch (scenario)
        {
            case "already-running":
                _ = await Ask(host, Post("/sindri/services/stuck/run", json: null, human));
                return await Ask(host, Post("/sindri/services/stuck/run", json: null, human));
            case "scheduled-in-flight":
                Expect.True(host.App.Services.GetRequiredService<ServiceRunGuard>().TryClaim(ServiceName.From("heartbeat"), out var claim));
                using (claim)
                {
                    return await Ask(host, Post("/sindri/services/heartbeat/run", json: null, human));
                }

            default:   // too-many: the cap of jobs run (all but one echoes, then the stuck service); one more is refused with 429, SND-JOB-002
                for (var n = 0; n < JobLimits.MaxRunning - 1; n++)
                {
                    _ = await JobHost.StartEcho(host, lines: 10 + n, delayMs: JobHost.LongDelayMs);
                }

                _ = await Ask(host, Post("/sindri/services/stuck/run", json: null, human));
                return await Ask(host, Post("/sindri/services/heartbeat/run", json: null, human));
        }
    }

    private static async Task<string> PauseOrResume(GoldenHost host, string verb, string scenario)
    {
        var service = scenario == "unknown-service" ? "ghost" : "heartbeat";
        var caller = scenario == "refused-session" ? Session : JobHost.Human(host);
        return await Ask(host, Post($"/sindri/services/{service}/{verb}", json: null, caller));
    }

    private static async Task<string> Services(GoldenHost host, string scenario, CancellationToken ct)
    {
        _ = await Ask(host, Post(scenario == "paused" ? "/sindri/services/heartbeat/pause" : "/sindri/services/stuck/run", json: null, JobHost.Human(host)));

        await ViewScenarios.CatchUp(host, ct);
        return await ViewScenarios.Read(host, "/sindri/services");
    }

    /// <summary>
    /// A scheduled run of 'heartbeat' failed a minute ago; a person runs it by hand and it succeeds. The next slot stays the scheduled one's plus the cadence
    /// (a manual run fills no slot), and the failure's needs-you row is gone (API P6 S5).
    /// </summary>
    private static async Task<string> AfterManualRun(GoldenHost host, string path, CancellationToken ct)
    {
        await ViewScenarios.Append(host, EventTypes.ServiceRan,
            new ServiceRan(Command(3), ServiceName.From("heartbeat"), host.Now().AddMinutes(-1), ServiceOutcome.Failed, "SND-SCHED-001"), "service.ran:golden-failed", ct);
        await ViewScenarios.CatchUp(host, ct);
        _ = await Ask(host, Post("/sindri/services/heartbeat/run", json: null, JobHost.Human(host)));
        await JobHost.Settle(host, ct);
        return await ViewScenarios.Read(host, path);
    }

    /// <summary>The profile names a service this build lacks: its run failed with SND-SVC-404, and its needs-you row has no primary action (it can only be paused).</summary>
    private static async Task<string> UnknownService(GoldenHost host, CancellationToken ct)
    {
        await ViewScenarios.Append(host, EventTypes.ServiceRan,
            new ServiceRan(Command(4), ServiceName.From("phantom"), host.Now().AddMinutes(-1), ServiceOutcome.Failed, SindriErrors.ServiceNotFound.Code), "service.ran:golden-unknown", ct);
        await ViewScenarios.CatchUp(host, ct);
        return await ViewScenarios.Read(host, "/sindri/needs-you");
    }

    /// <summary>The events of one manual run, as the stream sends them: started, two lines, finished, and the run's manual 'service.ran' naming the job.</summary>
    private static async Task<string> Lifecycle(GoldenHost host, CancellationToken ct)
    {
        var job = Job(1);
        await Started(host, job, "RunService", "heartbeat", ct, service: "heartbeat");
        await Progress(host, job, 1, "run heartbeat (manual)", ct);
        await Progress(host, job, 2, "second line", ct);
        await Finished(host, job, JobOutcome.Succeeded, "ok: worked", code: null, result: null, ct);
        await ViewScenarios.Append(host, EventTypes.ServiceRan,
            new ServiceRan(Command(1), ServiceName.From("heartbeat"), host.Now(), ServiceOutcome.Ok, Code: null, job), $"service.ran:heartbeat:manual:{job.Value}", ct);
        await ViewScenarios.CatchUp(host, ct);
        return await StreamScenarios.Frames(host, "/sindri/events?types=job.*,service.ran", JobHost.Human(host), ct);
    }

    private static async Task<string> List(GoldenHost host, string scenario, CancellationToken ct)
    {
        if (scenario is not ("empty" or "empty-unfiltered" or "bad-kind" or "bad-state"))
        {
            await Started(host, Job(1), "EchoJob", "echo", ct);
            await Started(host, Job(2), "RunService", "heartbeat", ct, service: "heartbeat");
            await Finished(host, Job(2), JobOutcome.Succeeded, "ok: worked", code: null, result: null, ct);
        }

        await ViewScenarios.CatchUp(host, ct);
        return await ViewScenarios.Read(host, scenario switch
        {
            "by-kind" => "/sindri/jobs?kind=RunService",
            "empty-unfiltered" => "/sindri/jobs",
            "bad-kind" => "/sindri/jobs?kind=NoSuchJob",
            "bad-state" => "/sindri/jobs?state=paused",
            _ => "/sindri/jobs?state=running",
        });
    }

    private static async Task<string> Cancel(GoldenHost host, string scenario, CancellationToken ct)
    {
        var human = JobHost.Human(host);
        switch (scenario)
        {
            case "not-found":
                await ViewScenarios.CatchUp(host, ct);   // CancelJob reads the views: they must have reached the head
                return await Ask(host, Post($"/sindri/jobs/{_unknownJob}/cancel", json: null, human));
            case "refused-session":
                return await Ask(host, Post($"/sindri/jobs/{Job(1).Value}/cancel", json: null, Session));
            case "already-ended":
                await Started(host, Job(1), "EchoJob", "echo", ct);
                await Finished(host, Job(1), JobOutcome.Succeeded, "echoed 1", code: null, result: null, ct);
                break;
            default:
                await Started(host, Job(1), "EchoJob", "echo", ct);
                break;
        }

        await ViewScenarios.CatchUp(host, ct);
        return await Ask(host, Post($"/sindri/jobs/{Job(1).Value}/cancel", json: null, human));
    }

    // How the job of 'GET /jobs/{id}' got to the state each scenario shows: the events that follow its 'job.started'.
    private static readonly Dictionary<string, Func<GoldenHost, JobId, CancellationToken, Task>> _shapes = new(StringComparer.Ordinal)
    {
        ["running"] = static (_, _, _) => Task.CompletedTask,
        ["succeeded"] = static async (h, j, ct) =>
        {
            await Progress(h, j, 1, "echo 1", ct);
            await Finished(h, j, JobOutcome.Succeeded, "echoed 1", code: null, result: null, ct);
        },
        ["result"] = static (h, j, ct) => Finished(h, j, JobOutcome.Succeeded, "echoed 2", code: null, JsonDocument.Parse("{\"lines\":2}").RootElement.Clone(), ct),
        ["failed"] = static (h, j, ct) => Finished(h, j, JobOutcome.Failed, "it broke", "SND-SCHED-001", result: null, ct),
        ["interrupted"] = static (h, j, ct) => Finished(h, j, JobOutcome.Failed, "interrupted: the process stopped while it ran", "SND-JOB-001", result: null, ct),
        ["cancelled"] = static (h, j, ct) => ViewScenarios.Append(h, EventTypes.JobCancelled, new JobCancelled(Command(2), j, "human:cli"), $"job.cancelled:{j.Value}", ct),
        ["progress-capped"] = static async (h, j, ct) =>
        {
            for (var n = 1; n <= 250; n++)
            {
                await Progress(h, j, n, "line " + n.ToString(CultureInfo.InvariantCulture), ct);
            }
        },
        ["context-backtest"] = static async (h, j, ct) =>
        {
            await Progress(h, j, 1, "corpus: 5 samples", ct);
            await Progress(h, j, 2, "arm graph 5/5", ct);
            var result = new RunContextBacktestResult(5, SmallSample: true, LeftOut: 1,
                [new BacktestArmLine("brief", NotAvailable: null, Verdicts.OwnerDecision, Leaky: false, new BacktestArmScore(5, 0.5, 0.4, 0.3, 1.0, 0)),
                 new BacktestArmLine("graph", NotAvailable: null, Verdicts.OwnerDecision, Leaky: false, new BacktestArmScore(5, 0.0, NodePrecision: null, 0.0, 1.0, 0)),
                 new BacktestArmLine("linear", "S-6", Overall: null, Leaky: false, Score: null)],
                Verdicts.OwnerDecision, "<home>/context/eval/backtest/20260101T000000Z.json", ["clusters unavailable: 1 of 5 sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)"]);
            var element = JsonSerializer.SerializeToElement(result, h.App.Services.GetRequiredService<JsonSerializerOptions>());
            await Finished(h, j, JobOutcome.Succeeded, "5 samples, brief+graph, " + Verdicts.OwnerDecision, code: null, element, ct);
        },
        ["ansi-escaped"] = static (h, j, ct) => Progress(h, j, 1, JobLines.Clean(h.App.Services.GetRequiredService<ITextScrubber>(), "\u001b[31mred\u001b[0m \u001b]0;title\u0007done"), ct),   // the line the real writer records
        ["stalled"] = static (h, _, ct) => ViewScenarios.Stall(h, ct),
    };

    private static async Task<string> JobBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        var job = Job(1);
        var path = $"/sindri/jobs/{job.Value}";
        if (string.Equals(scenario, "not-found", StringComparison.Ordinal) || string.Equals(scenario, "bad-id", StringComparison.Ordinal))
        {
            return await ViewScenarios.Read(host, scenario == "bad-id" ? "/sindri/jobs/not-a-job" : $"/sindri/jobs/{_unknownJob}");
        }

        var repo = scenario == "session-other-repo" ? "demo" : null;
        var backtest = string.Equals(scenario, "context-backtest", StringComparison.Ordinal);
        await Started(host, job, backtest ? "RunContextBacktest" : "EchoJob", backtest ? "backtest" : "echo", ct, repo);
        if (_shapes.TryGetValue(scenario, out var shape))
        {
            await shape(host, job, ct);
        }

        await ViewScenarios.CatchUp(host, ct);
        return repo is null
            ? await ViewScenarios.Read(host, path)
            : await Ask(host, Get(path, [.. Session, (SindriHeaders.Repo, "other")]));
    }

    private static Task Started(GoldenHost host, JobId job, string kind, string key, CancellationToken ct, string? repo = null, string? service = null) =>
        ViewScenarios.Append(host, EventTypes.JobStarted,
            new JobStarted(Command(1), job, JobKind.From(kind), JobKey.From(key), Item: null, repo is null ? null : RepoName.From(repo), service is null ? null : ServiceName.From(service)),
            $"job.started:{job.Value}", ct);

    private static Task Progress(GoldenHost host, JobId job, int round, string line, CancellationToken ct) =>
        ViewScenarios.Append(host, EventTypes.JobProgress, new JobProgress(Command(1), job, round, line), $"job.progress:{job.Value}:{round.ToString(CultureInfo.InvariantCulture)}", ct);

    private static Task Finished(GoldenHost host, JobId job, JobOutcome outcome, string summary, string? code, JsonElement? result, CancellationToken ct) =>
        ViewScenarios.Append(host, EventTypes.JobFinished, new JobFinished(Command(1), job, outcome, summary, code, result), $"job.finished:{job.Value}", ct);
}
