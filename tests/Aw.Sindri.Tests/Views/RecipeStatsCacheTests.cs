using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The recipe stats cache in views.db (API P12): what is kept is served until it is a run's start or end, an hour or a rebuild old, and a count made before the projector folded past it is never kept.</summary>
[Lane(TestLane.Integration)]
public sealed class RecipeStatsCacheTests : IDisposable
{
    private static readonly RecipeId _recipe = RecipeId.From("workflow.item");
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private readonly SqliteViews _views;
    private readonly SqliteRecipeStatsCache _cache;
    private long _sequence;

    public RecipeStatsCacheTests()
    {
        _views = new SqliteViews(Path.Combine(_home.Path, "home"));
        _cache = new SqliteRecipeStatsCache(_views.Database, _views.Reader, _views.Options);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _views.Dispose();
        _home.Dispose();
    }

    private static RecipeStats Stats(string? hash = null, int runs = 3) =>
        new("workflow.item", hash, "30d", ChildOnly: false, runs, Done: 2, Failed: 1, Cancelled: 0, Outcomes: null, HumanRate: 0.5, MedianSeconds: 60, Tokens: null, BudgetHits: 0,
            HumanTime: null, ByState: [], Buckets: [], AsOf: DateTimeOffset.UnixEpoch);

    private async Task Fold<TPayload>(string type, TPayload payload)
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        var applied = (await _views.Store.Apply([Recorded.Of(_h.Events, type, payload, ++_sequence)], [new RecipeStatsProjection(_h.Events)], _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
    }

    private string Json(RecipeStats? stats) => System.Text.Json.JsonSerializer.Serialize(stats, _views.Options);

    private Task Workflow(string type, string? outcome)
    {
        var delta = new WorkflowEventDelta(Frame: null, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return Fold(type, new WorkflowEventPayload("T-1/item/1", "produce", "done", "step.completed", Outcome: null, Run: null, detail));
    }

    private async Task<RecipeStats?> Get(string? hash = null) => (await _cache.Get(_recipe, hash, StatsWindow.ThirtyDays, _h.Clock.GetUtcNow(), Token)).Value;

    [Fact]
    public async Task What_is_kept_is_served_under_its_recipe_window_and_hash_only()
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        _ = (await _cache.Put(Stats(), head: 0, _h.Clock.GetUtcNow(), Token)).Value;

        Assert.Equal(Json(Stats()), Json(await Get()));   // collections compare by reference, so by value through the wire shape
        Assert.Null(await Get(hash: "sha256:" + new string('a', 64)));
    }

    [Fact]
    public async Task An_entry_older_than_an_hour_is_not_served()
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        _ = (await _cache.Put(Stats(), head: 0, _h.Clock.GetUtcNow(), Token)).Value;

        _h.Clock.Advance(IRecipeStatsCache.MaxAge);

        Assert.Null(await Get());
    }

    [Fact]
    public async Task A_count_made_before_the_projector_folded_past_it_is_not_kept()
    {
        await Workflow(EventTypes.WorkflowStarted, outcome: null);
        await Workflow(EventTypes.WorkflowTransition, outcome: "resolved");   // the projector's cursor is 2

        _ = (await _cache.Put(Stats(), head: 1, _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(await Get());

        _ = (await _cache.Put(Stats(), head: 2, _h.Clock.GetUtcNow(), Token)).Value;
        Assert.NotNull(await Get());
    }

    [Fact]
    public async Task A_run_starting_or_ending_empties_the_cache_and_a_move_does_not()
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        _ = (await _cache.Put(Stats(), head: 0, _h.Clock.GetUtcNow(), Token)).Value;

        await Workflow(EventTypes.WorkflowTransition, outcome: null);
        var kept = await Get();
        await Workflow(EventTypes.WorkflowStarted, outcome: null);
        var afterStart = await Get();
        _ = (await _cache.Put(Stats(), head: _sequence, _h.Clock.GetUtcNow(), Token)).Value;
        await Workflow(EventTypes.WorkflowTransition, outcome: "resolved");
        var afterEnd = await Get();

        Assert.NotNull(kept);
        Assert.Null(afterStart);
        Assert.Null(afterEnd);
    }

    [Fact]
    public async Task A_rebuild_empties_the_cache()
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        _ = (await _cache.Put(Stats(), head: 0, _h.Clock.GetUtcNow(), Token)).Value;

        _ = (await _views.Store.Reset(ViewNames.All, ViewSchema.Version, _h.Clock.GetUtcNow(), 0, Token)).Value;

        Assert.Null(await Get());
    }

    [Fact]
    public async Task With_no_views_file_nothing_is_kept_and_nothing_is_served()
    {
        var put = await _cache.Put(Stats(), head: 0, _h.Clock.GetUtcNow(), Token);

        Assert.True(put.IsSuccess);
        Assert.Null(await Get());
        Assert.False(File.Exists(_views.Path));
    }
}
