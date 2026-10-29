using Aw.Context.Core.Linking;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The facade's stored-state gap check: an owned index source past no recent window, or a configured intent source with no successful sync, read without probing any source.</summary>
public sealed class SyncGapTests
{
    private const string OnyxSettings = """{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"env:K","version":"v4.9.0"}}}""";
    private static readonly SourceId _onyx = SourceId.From("onyx");
    private static readonly SourceId _linear = SourceId.From("linear");
    private readonly FakeIntentStore _intents = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<bool> Gap(string? settings) =>
        (await Engines.Over(new FixedSourceCatalog(), intentStore: _intents, indexSync: _index).HasSyncGap(Engines.Human(settings), Ct)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_configured_source_that_never_ran_is_a_gap() =>
        Assert.True(await Gap(OnyxSettings));

    [Fact, Trait("Lane", "unit")]
    public async Task A_configured_source_whose_only_syncs_failed_is_still_a_gap()
    {
        _ = await _intents.RecordFailure(_onyx, "SND-CONTEXT-010", Ct);

        Assert.True(await Gap(OnyxSettings));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_synced_source_is_not_a_gap()
    {
        _ = await _intents.CommitPage(new IntentPage(_onyx, [], [], Cursor: null, new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero)), Ct);

        Assert.False(await Gap(OnyxSettings));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_configured_intent_source_is_no_gap()
    {
        Assert.False(await Gap("{}"));
        Assert.False(await Gap(settings: null));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_source_that_never_committed_a_page_is_a_gap()
    {
        _index.Standing = [SourceProgress.From(_linear, phase: null, lastSync: null, lastCode: null)];

        Assert.True(await Gap("{}"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_source_whose_ticks_only_failed_is_still_a_gap()
    {
        _index.Standing = [SourceProgress.From(_linear, phase: null, lastSync: new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero), lastCode: "SND-CONTEXT-010")];

        Assert.True(await Gap("{}"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_source_past_its_recent_window_is_not_a_gap()
    {
        _index.Standing = [SourceProgress.From(_linear, SyncPhase.Backfill, new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero), lastCode: null)];

        Assert.False(await Gap("{}"));
    }
}
