using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The 'context-sync' scheduled service and what it leaves behind (context spec §8.8, S-5B): the intent layer synced from Onyx, the
/// 'context.sources.synced' event with its link counts, the sync block of 'context sources status', and the doctor row 'context:sync'.
/// </summary>
public sealed class ContextSyncTests : IDisposable
{
    private const string KeyVariable = "SINDRI_TEST_ONYX_KEY";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task ContextSync_runs_only_when_named_under_services()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var named = await Approved(onyx, ct, services: true);
        await using var unnamed = await Approved(onyx, ct, services: false);

        var listed = await named.Http.Ask(Get("/sindri/services", Human(named.Token.Value)));
        var absent = await unnamed.Http.Ask(Get("/sindri/services", Human(unnamed.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Contains("\"name\":\"context-sync\"", listed.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"context-sync\"", absent.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task ContextSync_records_context_sources_synced_with_link_counts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);

        var result = await RunSync(host, DateTimeOffset.UnixEpoch, ct);
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextSourcesSynced, ct);

        Assert.True(result.IsSuccess);
        using var payload = JsonDocument.Parse(recorded.Value!.Envelope.Payload);
        Assert.Equal("onyx", payload.RootElement.GetProperty("source").GetString());
        var links = payload.RootElement.GetProperty("links");
        Assert.True(links.GetProperty("linked").GetInt32() > 0);
        Assert.True(links.GetProperty("records").GetInt32() > 0);
        Assert.DoesNotContain("Toggle split orientation", recorded.Value.Envelope.Payload, StringComparison.Ordinal);   // counts and codes only, never record text
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task ContextSync_event_key_is_unique_per_source_and_slot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);

        _ = await RunSync(host, DateTimeOffset.UnixEpoch, ct);
        var key = (await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextSourcesSynced, ct)).Value!.IdempotencyKey;

        Assert.Matches(@"^context\.sources\.synced:\d+:onyx:1970-01-01T00:00:00\.0000000\+00:00$", key);   // the docs refresh's key names its source too, so the two never collide on a slot
        Assert.Equal("context.sources.synced:<epoch>:<source>:<slot>", EventCatalog.All.Single(e => e.Type == EventTypes.ContextSourcesSynced).KeyShape);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task SourcesStatus_shows_last_sync_links_decisions_queued_per_intent_source()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);
        _ = await RunSync(host, DateTimeOffset.UnixEpoch, ct);

        var sources = await host.Http.Ask(Get("/sindri/context/sources", Human(host.Token.Value)));

        using var view = JsonDocument.Parse(sources.Body);
        var row = view.RootElement.GetProperty("sources").EnumerateArray().Single(r => r.GetProperty("source").GetString() == "onyx");
        var sync = row.GetProperty("sync");
        Assert.Equal("ok", sync.GetProperty("state").GetString());
        Assert.True(sync.GetProperty("linked").GetInt32() > 0);
        Assert.Equal(JsonValueKind.String, sync.GetProperty("lastSync").ValueKind);
        Assert.True(sync.TryGetProperty("decisions", out _) && sync.TryGetProperty("queued", out _));
        Assert.DoesNotContain(view.RootElement.GetProperty("sources").EnumerateArray().Where(r => r.GetProperty("source").GetString() == "repo-docs"), r => r.TryGetProperty("sync", out var s) && s.ValueKind != JsonValueKind.Null);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task Doctor_context_sync_warns_when_never_synced_or_last_error()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);

        var never = await Doctor(host, ct);
        _ = await RunSync(host, DateTimeOffset.UnixEpoch, ct);
        var synced = await Doctor(host, ct);
        await onyx.DisposeAsync();
        _ = await RunSync(host, DateTimeOffset.UnixEpoch.AddMinutes(15), ct);
        var failed = await Doctor(host, ct);

        Assert.Equal(CheckState.Warn, never.State);
        Assert.Contains("never synced", never.Detail, StringComparison.Ordinal);
        Assert.Equal(CheckState.Ok, synced.State);
        Assert.Equal(CheckState.Warn, failed.State);
        Assert.Contains("SND-CONTEXT-010", failed.Detail, StringComparison.Ordinal);
        Assert.Contains("next context-sync slot", never.Fix, StringComparison.Ordinal);   // the service is named: its slot syncs without a command
        Assert.Contains("sindri context sources sync", never.Fix, StringComparison.Ordinal);   // and the job route runs one by hand (API P6)
        Assert.Contains("sindri context sources sync", failed.Fix, StringComparison.Ordinal);
        Assert.DoesNotContain("run it once", never.Fix, StringComparison.Ordinal);
        Assert.Contains("retries", failed.Fix, StringComparison.Ordinal);   // a failed run's fix does not send the operator to name a service that is named
        Assert.DoesNotContain("name the context-sync service", failed.Fix, StringComparison.Ordinal);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task Doctor_context_sync_fix_names_the_service_only_when_the_profile_lacks_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct, services: false);

        var never = await Doctor(host, ct);

        Assert.Contains("name the context-sync service", never.Fix, StringComparison.Ordinal);
        Assert.Contains("sindri context sources sync", never.Fix, StringComparison.Ordinal);
        Assert.DoesNotContain("run it once", never.Fix, StringComparison.Ordinal);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task A_sindri_item_id_has_no_tracker_record_until_S6_links_items_and_a_tracker_key_is_answered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);
        _ = await RunSync(host, DateTimeOffset.UnixEpoch, ct);

        var byId = await host.Http.Ask(Get("/sindri/context/query?item=ITM-00000000000000000000000000000001", Human(host.Token.Value)));
        var byKey = await host.Http.Ask(Get("/sindri/context/query?item=SPL-103", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (byId.Status, byId.Code));
        Assert.Contains("no synced record is linked", byId.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, byKey.Status);
        Assert.Contains("\"anchors\":[\"record:linear:issue/", byKey.Body, StringComparison.Ordinal);
    }

    private static async Task<Kernel.Result<ScheduledResult>> RunSync(GoldenHost host, DateTimeOffset slot, CancellationToken ct)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-sync");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        return await sync.Run(new ScheduledRun(grant, slot), ct);
    }

    private static async Task<DoctorCheck> Doctor(GoldenHost host, CancellationToken ct)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<IService<RunDoctorRequest, RunDoctorResponse>>().Handle(new RunDoctorRequest(), ct);
        return report.Value.Checks.Single(c => c.Name == "context:sync");
    }

    /// <summary>A host whose approved profile maps the fixture map's repo and names Onyx behind an env: pointer, with the fixture map imported and repo-docs refreshed.</summary>
    private async Task<GoldenHost> Approved(FakeOnyxServer onyx, CancellationToken ct, bool services = true)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {FixtureMap.Repo}, path: \"{_repo.Path}\" }}\n"
            + "context:\n  docs: { globs: [\"docs/**/*.md\"] }\n  sources:\n"
            + $"    onyx: {{ url: \"{onyx.Url}\", token: \"env:{KeyVariable}\", version: \"{FakeOnyxServer.PinnedVersion}\" }}\n"
            + "    linear: { teams: [SPL] }\n"
            + (services ? "services:\n  context-sync: { every: 15m }\n" : string.Empty);
        var host = await GoldenHost.Start(temporary: true, ct, yaml, new Dictionary<string, string>(StringComparer.Ordinal) { [KeyVariable] = onyx.Token });
        var hash = (await host.LiveHash(ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        await ContextMapApiTests.Import(host);
        return host;
    }
}
