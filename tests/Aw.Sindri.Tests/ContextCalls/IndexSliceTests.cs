using Aw.Context.Core;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The index status read and the purge command (owned-index spec §5.8, §10) over a fake <see cref="IIndexSync"/> behind the engine facade.</summary>
public sealed class IndexSliceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task ApproveProfile() => _ = await _h.Profiles.Save(SliceHarness.Profile("5m"), Ct);

    private ContextScopes Scopes() => new(_h.Profiles, _h.Callers);

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_each_feeds_phase_milestones_last_tick_and_last_code()
    {
        await ApproveProfile();
        _index.Standing =
        [
            SourceProgress.From(SourceId.From("linear"), SyncPhase.Incremental, _now, lastCode: null),
            SourceProgress.From(SourceId.From("github"), SyncPhase.Recent, lastSync: null, "SND-CONTEXT-010"),
            SourceProgress.From(SourceId.From("slack"), phase: null, lastSync: null, lastCode: null),
        ];
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(Scopes(), EngineProxy.Of(_index)).Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsSuccess);
        Assert.Equal(
            [
                new IndexSourceRow("linear", "incremental", Usable: true, Complete: true, _now, LastCode: null),
                new IndexSourceRow("github", "recent", Usable: false, Complete: false, LastSync: null, "SND-CONTEXT-010"),
                new IndexSourceRow("slack", Phase: null, Usable: false, Complete: false, LastSync: null, LastCode: null),
            ],
            view.Value.Sources);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_carries_the_stored_count_of_a_source_that_is_re_reading_and_lists_the_projects_feed_as_its_own_row()
    {
        await ApproveProfile();
        _index.Standing =
        [
            SourceProgress.From(SourceId.From("linear"), SyncPhase.Recent, lastSync: null, lastCode: null, stored: 1200),
            SourceProgress.From(SourceId.From("linear-projects"), SyncPhase.Incremental, _now, lastCode: null),
        ];
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(Scopes(), EngineProxy.Of(_index)).Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsSuccess);
        Assert.Equal(
            [
                new IndexSourceRow("linear", "recent", Usable: false, Complete: false, LastSync: null, LastCode: null, ReReading: 1200),
                new IndexSourceRow("linear-projects", "incremental", Usable: true, Complete: true, _now, LastCode: null),
            ],
            view.Value.Sources);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_carries_where_concept_synthesis_stands_with_its_coverage()
    {
        await ApproveProfile();
        _index.Standing = [SourceProgress.From(SourceId.From("linear"), SyncPhase.Incremental, _now, lastCode: null)];
        _index.Concepts = new ConceptStatus(Enabled: true, Concepts: 101, MultiSource: 34, Queued: 3, Covered: 300, Watermark: 8812, Embedded: 550, Pending: 12);
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(Scopes(), EngineProxy.Of(_index)).Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsSuccess);
        Assert.Equal(new IndexConceptsView(Enabled: true, Concepts: 101, MultiSource: 34, Queued: 3, Covered: 300, Embedded: 550, Pending: 12, Watermark: 8812, Coverage: 300d / 550), view.Value.Concepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_coverage_is_0_while_nothing_is_embedded()
    {
        await ApproveProfile();
        _index.Concepts = new ConceptStatus(Enabled: true, Concepts: 0, MultiSource: 0, Queued: 0, Covered: 0, Watermark: 0, Embedded: 0, Pending: 5);
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(Scopes(), EngineProxy.Of(_index)).Handle(new GetIndexStatusRequest(), Ct);

        Assert.Equal(0d, view.Value.Concepts?.Coverage);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_whose_concepts_read_failed_still_lists_the_feeds_and_carries_the_code()
    {
        await ApproveProfile();
        _index.Standing = [SourceProgress.From(SourceId.From("linear"), SyncPhase.Incremental, _now, lastCode: null)];
        _index.ConceptsFailure = ContextErrors.StoreUnavailable.With("the concept store can't be opened");
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(Scopes(), EngineProxy.Of(_index)).Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsSuccess);
        Assert.Equal("linear", Assert.Single(view.Value.Sources).Source);
        Assert.Equal(new IndexConceptsView(Enabled: true, Code: ContextErrors.StoreUnavailable.Code), view.Value.Concepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_without_an_approved_profile_is_the_profile_error()
    {
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(Scopes(), EngineProxy.Of(_index)).Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsError);
        Assert.Equal(SindriErrors.ProfileNotApproved.Code, view.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_without_confirm_counts_what_the_source_holds_and_deletes_nothing()
    {
        _index.Held = new SourceCounts(8200, 31000, 31000);
        using var caller = _h.Callers.EnterService("test");

        var view = await new PurgeIndexSourceService(EngineProxy.Of(_index)).Handle(new PurgeIndexSourceRequest("github"), Ct);

        Assert.True(view.IsSuccess);
        Assert.Equal(new IndexPurgeView("github", 8200, 31000, 31000, Purged: false), view.Value);
        Assert.Empty(_index.Purged);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_with_confirm_deletes_the_source_and_answers_what_it_held()
    {
        _index.Held = new SourceCounts(2, 3, 1);
        using var caller = _h.Callers.EnterService("test");

        var view = await new PurgeIndexSourceService(EngineProxy.Of(_index)).Handle(new PurgeIndexSourceRequest("linear", Confirm: true), Ct);

        Assert.True(view.IsSuccess);
        Assert.Equal(new IndexPurgeView("linear", 2, 3, 1, Purged: true), view.Value);
        Assert.Equal([SourceId.From("linear")], _index.Purged);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_of_a_name_that_is_not_a_source_id_is_invalid_input_and_touches_nothing()
    {
        using var caller = _h.Callers.EnterService("test");

        var view = await new PurgeIndexSourceService(EngineProxy.Of(_index)).Handle(new PurgeIndexSourceRequest("Not A Source!", Confirm: true), Ct);

        Assert.True(view.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, view.Error.Code);
        Assert.Empty(_index.Purged);
    }
}
