using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// The views a query slice reads, in memory: the slice harness, an in-memory views store folded through the real
/// projections, and the projector's progress, published after each fold as the projector does after a page.
/// </summary>
internal sealed class ViewsHarness
{
    private readonly IReadOnlyList<IProjection> _projections;
    private long _sequence;

    public ViewsHarness()
    {
        Progress = new ProjectorProgress(Slice.Clock);
        _projections =
        [
            new ServicesProjection(Slice.Events),
            new ProfileProjection(Slice.Events),
            new RequestsProjection(Slice.Events, new PassThroughScrubber()),
            new ItemsProjection(Slice.Events, new PassThroughScrubber()),
            new ItemAliasesProjection(Slice.Events),
            new ItemTimelineProjection(SliceHarness.Json, new PassThroughScrubber()),
            new NeedsYouProjection(Slice.Events, new PassThroughScrubber()),
            new JobsProjection(Slice.Events),
            new LaunchesProjection(Slice.Events),
        ];
    }

    public SliceHarness Slice { get; } = new();

    public InMemoryViewStore Views { get; } = new(SliceHarness.Json);

    public ProjectorProgress Progress { get; }

    /// <summary>Folds one recorded <paramref name="type"/> event through the projections, then publishes the cursor at it.</summary>
    public async Task Fold<TPayload>(string type, TPayload payload)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = (await Views.Open(ViewSchema.Version, ct)).Value;
        var applied = (await Views.Apply([Recorded.Of(Slice.Events, type, payload, ++_sequence)], _projections, Slice.Clock.GetUtcNow(), ct)).Value;
        Assert.Null(applied.Stall);
        Publish(stalledAt: null);
    }

    /// <summary>Folds one recorded <paramref name="type"/> event about <paramref name="item"/>, as the ledger stores it with its item set.</summary>
    public async Task Fold<TPayload>(string type, TPayload payload, ItemId item)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = (await Views.Open(ViewSchema.Version, ct)).Value;
        var applied = (await Views.Apply([Recorded.Of(Slice.Events, type, payload, ++_sequence, item)], _projections, Slice.Clock.GetUtcNow(), ct)).Value;
        Assert.Null(applied.Stall);
        Publish(stalledAt: null);
    }

    /// <summary>Folds ledger events as they were recorded (the sequences are theirs), then publishes the views as caught up to the last.</summary>
    /// <param name="page">The recorded events, in order.</param>
    /// <returns>The task.</returns>
    public async Task Apply(IReadOnlyList<LedgerEvent> page)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = (await Views.Open(ViewSchema.Version, ct)).Value;
        var applied = (await Views.Apply(page, _projections, Slice.Clock.GetUtcNow(), ct)).Value;
        Assert.Null(applied.Stall);
        _sequence = page[^1].Sequence;
        Publish(stalledAt: null);
    }

    /// <summary>Publishes the projector at the last folded sequence, fresh or stalled at <paramref name="stalledAt"/>.</summary>
    public void Publish(long? stalledAt) => Progress.Publish(new ViewsStatus(_sequence, Math.Max(_sequence, stalledAt ?? 0), Slice.Clock.GetUtcNow(),
        Rebuilding: false, Stalled: stalledAt is not null, stalledAt, stalledAt is null ? null : "service.ran"));
}
