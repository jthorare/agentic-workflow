using System.Net;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Records;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// Map builds queue on one gate per host (MapBuildGate): a map build or backtest that has to wait for a running build says so on its own job
/// (a 'waiting for the map build of job X' progress line) and never runs graphify beside it (#1683).
/// </summary>
public sealed class MapBuildQueueTests : IDisposable
{
    private const int BoundSeconds = 20;
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly DirectoryInfo _locks = Directory.CreateTempSubdirectory("aw-queue-locks-");

    public void Dispose()
    {
        _repo.Dispose();
        _locks.Delete(recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_build_started_while_a_refresh_builds_waits_and_says_which_job_it_waits_for()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clusters = new FakeClusterSource { Hold = hold.Task };
        var model = new FakeModelPort { Fail = static _ => ContextErrors.SourceUnavailable.With("no agent CLI in the test") };
        await using var host = await ContextHosts.Approved(_repo, sessions: false, Token, labeling: true, extra: services => Fakes(services, clusters, model));
        try
        {
            var refresh = await StartRefresh(host);
            await Until("the refresh job is inside graphify", () => Task.FromResult(clusters.Active == 1));

            var build = await StartBuild(host);
            var line = $"waiting for the map build of job {refresh.JobId.Value}";
            await Until($"the build job {build} reports '{line}'", async () => await Reported(host, build, line));
            var (active, asked) = (clusters.Active, clusters.Asked.Count);

            hold.SetResult();
            await JobHost.Settle(host, Token);
            Assert.Equal((1, 1), (active, asked));   // while the refresh held graphify, the build had not entered it
            Assert.Equal((1, 1), (clusters.MostActive, clusters.Asked.Count));   // and after the release it reused the refresh's draft instead of running graphify again (#1696)
            Assert.True(await Finished(host, build, "was built from these heads while this build waited, so it was not built again"), $"the build job {build} does not report the reused draft");
        }
        finally
        {
            _ = hold.TrySetResult();
        }
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_backtest_waits_for_a_running_map_build_before_it_runs_graphify()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clusters = new FakeClusterSource { Hold = hold.Task };
        await using var host = await ContextHosts.Approved(_repo, sessions: false, Token, contextExtra: "  sources:\n    linear: { teams: [SPL] }\n", extra: services => Fakes(services, clusters, model: null));
        try
        {
            // why: the backtest's corpus is the Linear issues in the record store joined with the fixture repo's merges; the host's store starts empty, so seed the split-panes issues.
            // The eval cache lives in the host's fresh temporary home, so the first sample's cut is a cache miss and its as-of map asks the cluster source (a cached cut would never reach graphify).
            await using (var scope = host.App.Services.CreateAsyncScope())
            {
                var seeded = await scope.ServiceProvider.GetRequiredService<IRecordStore>().Commit(RecordSamples.Batch(RecordSamples.Linear, 1, [.. LinearIssueSeed.From(issues: null)]), Token);
                Assert.True(seeded.IsSuccess, seeded.IsError ? seeded.Error.Message : null);
            }

            var build = await StartBuild(host);
            await Until("the build job is inside graphify", () => Task.FromResult(clusters.Active == 1));

            var backtest = await Start(host, "/sindri/context/eval/backtest", """{"arms":"docs","limit":1}""");
            var line = $"waiting for the map build of job {build}";
            await Until($"the backtest job {backtest} reports '{line}'", async () =>
            {
                Assert.True(clusters.Active <= 1, "the backtest ran graphify beside the build that holds it");
                return await Reported(host, backtest, line);
            });
            var (active, asked) = (clusters.Active, clusters.Asked.Count);

            hold.SetResult();
            await JobHost.Settle(host, Token);
            Assert.Equal((1, 1), (active, asked));   // the backtest had not entered graphify while the build held it
            Assert.Equal(1, clusters.MostActive);
        }
        finally
        {
            _ = hold.TrySetResult();
        }
    }

    private void Fakes(IServiceCollection services, FakeClusterSource clusters, FakeModelPort? model)
    {
        // why: the jobs' builds would otherwise run the box's graphify, agent CLIs and heavy-job lock, which stall them under load
        _ = services.RemoveAll<IClusterSource>();
        _ = services.AddSingleton<IClusterSource>(clusters);
        if (model is not null)
        {
            _ = services.AddSingleton<IModelPort>(model);
        }

        _ = services.RemoveAll<HeavyLockSettings>();
        _ = services.AddSingleton(new HeavyLockSettings(Path.Combine(_locks.FullName, "heavy-job.lock"), 2, Held: false));
    }

    private static async Task<JobStartedResponse> StartRefresh(GoldenHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        using var caller = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("context-map-refresh");
        var started = await scope.ServiceProvider.GetRequiredService<Kernel.Services.IService<RefreshContextMapRequest, JobStartedResponse>>().Handle(new RefreshContextMapRequest(), Token);
        Assert.True(started.IsSuccess, started.IsError ? $"{started.Error.Code}: {started.Error.Message}" : null);
        return started.Value;
    }

    private static Task<string> StartBuild(GoldenHost host) => Start(host, "/sindri/context/map/build", "{}");

    private static async Task<string> Start(GoldenHost host, string route, string body)
    {
        var started = await host.Http.Ask(Post(route, body, Human(host.Token.Value)));
        Assert.True(started.Status == HttpStatusCode.OK, started.Body);
        return JsonDocument.Parse(started.Body).RootElement.GetProperty("jobId").GetString()!;
    }

    // A job.progress event of that job carrying exactly that line (the payload names the job and the line).
    private static async Task<bool> Reported(GoldenHost host, string job, string line)
    {
        var events = (await host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 5000, Token)).Value;
        return events.Any(e => e.Envelope.Type == EventTypes.JobProgress && e.Envelope.Payload.Contains(job, StringComparison.Ordinal) && e.Envelope.Payload.Contains($"\"{line}\"", StringComparison.Ordinal));
    }

    // The job.finished event of that job, its result carrying that text.
    private static async Task<bool> Finished(GoldenHost host, string job, string text)
    {
        var events = (await host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 5000, Token)).Value;
        return events.Any(e => e.Envelope.Type == EventTypes.JobFinished && e.Envelope.Payload.Contains(job, StringComparison.Ordinal) && e.Envelope.Payload.Contains(text, StringComparison.Ordinal));
    }

    private static async Task Until(string what, Func<Task<bool>> holds)
    {
        for (var i = 0; !await holds(); i++)
        {
            Assert.True(i < BoundSeconds * 10, $"gave up after {BoundSeconds} s waiting until {what}");
            await Task.Delay(100, Token);
        }
    }
}
