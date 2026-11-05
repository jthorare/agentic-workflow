using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Infrastructure.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The refresh job on a started host: the 'context-map-refresh' service starts it through the command chain as itself, with no challenge or lease prompt (sindri-api's request, owner decision 2026-10-19).</summary>
public sealed class RefreshContextMapHostTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly DirectoryInfo _locks = Directory.CreateTempSubdirectory("aw-refresh-locks-");

    public void Dispose()
    {
        _repo.Dispose();
        _locks.Delete(recursive: true);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_service_caller_starts_RefreshContextMap_with_no_challenge_and_the_job_is_recorded_as_that_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = new FakeModelPort { Fail = static _ => ContextErrors.SourceUnavailable.With("no agent CLI in the test") };
        await using var host = await ContextHosts.Approved(_repo, sessions: false, ct, labeling: true, extra: services =>
        {
            // why: the job's build and cleanup would otherwise run the box's graphify, agent CLIs and heavy-job lock, which stall it under load
            _ = services.RemoveAll<IClusterSource>();
            _ = services.AddSingleton<IClusterSource>(new FakeClusterSource());
            _ = services.AddSingleton<IModelPort>(model);
            _ = services.RemoveAll<HeavyLockSettings>();
            _ = services.AddSingleton(new HeavyLockSettings(Path.Combine(_locks.FullName, "heavy-job.lock"), 2, Held: false));
        });

        Kernel.Result<JobStartedResponse> started;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            using var caller = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("context-map-refresh");
            started = await scope.ServiceProvider.GetRequiredService<Kernel.Services.IService<RefreshContextMapRequest, JobStartedResponse>>().Handle(new RefreshContextMapRequest(), ct);
        }

        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        var begun = await ledger.Latest(EventTypes.JobStarted, ct);
        Assert.True(started.IsSuccess, started.IsError ? $"{started.Error.Code}: {started.Error.Message}" : null);
        Assert.Equal("service:context-map-refresh", begun.Value!.Envelope.Actor);
        await WaitForJobToEnd(ledger, ct);
    }

    // #1955: on a box whose record rows held '' versions, every record read threw the version rule (#1952, #1954) and a refresh job was seen 'running' at 259 rounds.
    // The throw climbs out of the build and the job ends failed with it on the first attempt: no retry, no loop.
    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_refresh_whose_record_read_throws_the_version_rule_ends_failed_after_one_attempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var records = ThrowingRecordStore.Create();
        var model = new FakeModelPort { Fail = static _ => ContextErrors.SourceUnavailable.With("no agent CLI in the test") };
        await using var host = await ContextHosts.Approved(_repo, sessions: false, ct, labeling: true, extra: services =>
        {
            _ = services.RemoveAll<IClusterSource>();
            _ = services.AddSingleton<IClusterSource>(new FakeClusterSource());
            _ = services.AddSingleton<IModelPort>(model);
            _ = services.RemoveAll<HeavyLockSettings>();
            _ = services.AddSingleton(new HeavyLockSettings(Path.Combine(_locks.FullName, "heavy-job.lock"), 2, Held: false));
            _ = services.RemoveAll<IRecordStore>();
            _ = services.AddSingleton(records);
        });

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            using var caller = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("context-map-refresh");
            var started = await scope.ServiceProvider.GetRequiredService<Kernel.Services.IService<RefreshContextMapRequest, JobStartedResponse>>().Handle(new RefreshContextMapRequest(), ct);
            Assert.True(started.IsSuccess, started.IsError ? $"{started.Error.Code}: {started.Error.Message}" : null);
        }

        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        await WaitForJobToEnd(ledger, ct);
        var events = (await ledger.ReadAfter(0, 5000, ct)).Value ?? [];
        var finished = Json(events.Single(static e => e.Envelope.Type == EventTypes.JobFinished).Envelope.Payload);
        var lines = events.Where(static e => e.Envelope.Type == EventTypes.JobProgress).Select(static e => Json(e.Envelope.Payload).GetProperty("line").GetString()).ToList();
        Assert.True(((ThrowingRecordStore)(object)records).Listed > 0, "the build never read the records: " + finished.GetRawText());
        Assert.Equal(("failed", SindriErrors.ServiceFailed.Code), (finished.GetProperty("outcome").GetString(), finished.GetProperty("code").GetString()));
        Assert.Contains("A source version is 1 to 128 characters", finished.GetProperty("summary").GetString(), StringComparison.Ordinal);
        _ = Assert.Single(lines, static l => l == "building");
        Assert.DoesNotContain(lines, static l => l!.StartsWith("retrying", StringComparison.Ordinal));
        Assert.True(lines.Count < 20, "progress lines: " + string.Join(" | ", lines));
    }

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement.Clone();

    // The job ends on its own (a failure is fine here: the start is what this test pins); wait so the host isn't disposed under it.
    private static async Task WaitForJobToEnd(ILedgerStore ledger, CancellationToken ct)
    {
        for (var i = 0; i < 160; i++)
        {
            if ((await ledger.Latest(EventTypes.JobFinished, ct)).Value is not null)
            {
                return;
            }

            await Task.Delay(250, ct);
        }

        var tail = (await ledger.ReadAfter(0, 500, ct)).Value ?? [];
        Assert.Fail("the refresh job didn't end; the ledger ends with: " + string.Join(", ", tail.TakeLast(6).Select(static e => e.Envelope.Type)));
    }
}
