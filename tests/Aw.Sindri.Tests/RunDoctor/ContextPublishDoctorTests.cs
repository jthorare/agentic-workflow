using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>The 'context:publish' doctor row (code index spec 6.4): overdue always warns, a stale map warns only once it has been stale for more than five minutes.</summary>
public sealed class ContextPublishDoctorTests
{
    private static readonly DateTimeOffset _published = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly ContextRepoName _repo = ContextRepoName.From("demo");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LedgerEvent Published() => new(
        1, Epoch: 1,
        new Kernel.Events.EventEnvelope<string>(new Guid(1, 0, 0, new byte[8]), EventTypes.ContextMapPublished, SchemaVersion: 1, _published, "service:context-map-refresh", Tenant: "t", Ring: 0, new Guid(2, 0, 0, new byte[8]), CausationId: null, TraceId: null,
            """{"repo":"demo","version":14,"reason":"TimedOut"}"""),
        "k1", Item: null);

    private static async Task<DoctorCheck> Row(MapPublishView view, DateTimeOffset now, string? indexCommit)
    {
        var outlooks = new ScriptedOutlooks();
        if (indexCommit is not null)
        {
            outlooks.Commits["demo"] = indexCommit;
        }

        var clock = new FakeTimeProvider(now);
        var engine = PublishStatusEngineProxy.Create(view);
        var reader = new MapPublishStatusReader(engine, outlooks, new ListLedger([Published()]), clock);
        var checks = new ContextEngineChecks(FakeContextEngine.Instance, new MapPublishDoctorCheck(reader, clock), new LinearDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))), new Features.CodeIndexJobs.CodeIndexDoctor(new CodeIndex.Testing.FakeCodeIndex(), new CodeIndex.Testing.FakeRepoHeads(), new InMemoryJobViews(), new CodeIndex.Testing.FakeEmbedder(), new CodeIndex.Testing.FakePins(), clock, new InMemoryProfiles()));
        var rows = await checks.Check(SliceHarness.Profile(), Ct);
        return rows.Single(static c => c.Name == MapPublishDoctorCheck.PublishName);
    }

    private static MapPublishView Current() => new([_repo], Hold: null, LatestDraft: null, Current: 14, [new MapHead(_repo, "1234567", OnDefault: true)]);

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_map_four_minutes_after_its_publish_is_ok_and_names_the_last_publish_reason()
    {
        var row = await Row(Current(), _published.AddMinutes(4), indexCommit: "abcdef0");

        Assert.Equal(CheckState.Ok, row.State);
        Assert.EndsWith("(last publish: TimedOut)", row.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_map_six_minutes_after_its_publish_warns_with_the_code_index_fix()
    {
        var row = await Row(Current(), _published.AddMinutes(6), indexCommit: "abcdef0");

        Assert.Equal(CheckState.Warn, row.State);
        Assert.StartsWith("sindri code-index status", row.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_held_map_past_its_deadline_always_warns()
    {
        var hold = new MapHold(15, _published, [new MapHead(_repo, "1234567", OnDefault: true)]);
        var view = new MapPublishView([_repo], hold, LatestDraft: 15, Current: 14, [new MapHead(_repo, "1234567", OnDefault: true)]);

        var row = await Row(view, _published.AddHours(3), indexCommit: null);

        Assert.Equal(CheckState.Warn, row.State);
    }
}
