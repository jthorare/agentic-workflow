using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>A configured intent source that has never synced: the history route shows the gap, the 'context-sync-gaps' service fills it through the command chain, the next read is clean (owner decision 2026-10-18).</summary>
public sealed class ContextSyncGapsHostTests : IDisposable
{
    private const string KeyVariable = "SINDRI_TEST_LINEAR_KEY";
    private const string History = "/sindri/context/history?anchor=feature%3Alayout.split-pane";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static JsonElement LinearRow(string body)
    {
        using var view = JsonDocument.Parse(body);
        return view.RootElement.GetProperty("searched").GetProperty("sources").EnumerateArray().Single(s => s.GetProperty("id").GetString() == "linear").Clone();
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Never_synced_source_reads_not_synced_161_then_the_service_syncs_it_as_itself_and_the_next_read_is_clean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var linear = await FakeLinearServer.Start();
        await using var host = await Approved(linear, ct);

        var before = await host.Http.Ask(Get(History, Human(host.Token.Value)));
        var first = await RunGaps(host, ct);
        await JobHost.Settle(host, ct);
        var after = await host.Http.Ask(Get(History, Human(host.Token.Value)));
        var second = await RunGaps(host, ct);
        var started = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.JobStarted, ct);

        var gap = LinearRow(before.Body);
        Assert.Equal(("unavailable", "not-synced", "SND-CONTEXT-161"), (gap.GetProperty("state").GetString(), gap.GetProperty("reason").GetString(), gap.GetProperty("code").GetString()));
        Assert.False(gap.TryGetProperty("sync", out _));
        Assert.False(first.Value.Idle);
        var healed = LinearRow(after.Body);
        Assert.True(healed.GetProperty("state").GetString() == "ok", healed.GetRawText());
        Assert.True(second.Value.Idle);
        Assert.Equal("service:context-sync-gaps", started.Value!.Envelope.Actor);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_service_is_a_built_in_with_no_profile_entry_and_a_tick_with_no_approved_profile_is_quiet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var linear = await FakeLinearServer.Start();
        await using var host = await Approved(linear, ct);

        var builtIns = host.App.Services.GetServices<BuiltInService>();

        Assert.Contains(builtIns, b => b.Name.Value == "context-sync-gaps");
    }

    private static async Task<Kernel.Result<ScheduledResult>> RunGaps(GoldenHost host, CancellationToken ct)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var gaps = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-sync-gaps");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        using var caller = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("context-sync-gaps");   // as the scheduler enters it
        return await gaps.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct);
    }

    private async Task<GoldenHost> Approved(FakeLinearServer linear, CancellationToken ct)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {FixtureMap.Repo}, path: \"{_repo.Path}\" }}\n"
            + "context:\n  docs: { globs: [\"docs/**/*.md\"] }\n  sources:\n"
            + $"    linear: {{ url: \"{linear.Url}\", token: \"env:{KeyVariable}\", teams: [SPL] }}\n";
        var host = await GoldenHost.Start(temporary: true, ct, yaml, new Dictionary<string, string>(StringComparer.Ordinal) { [KeyVariable] = linear.Token });
        var hash = (await host.LiveHash(ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        await ContextMapApiTests.Import(host);
        return host;
    }
}
