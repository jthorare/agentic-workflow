using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.context.lenses' (scoping spec C1): the multi-anchor lenses read in process as the service, cut to the instance's repo.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLensesInputTests
{
    internal sealed class StubLenses(Func<IReadOnlyList<string>, Result<LensesSetView>> answer) : IContextLenses
    {
        public List<(IReadOnlyList<string> Anchors, LensRange Range, ContextScope Scope)> Calls { get; } = [];

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken)
        {
            Calls.Add((anchors, range, scope));
            return ValueTask.FromResult(answer(anchors));
        }

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LensesView>> Lenses(string anchor, bool draft, int? depth, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<WholeMapView>> WholeMap(WholeMapQuery query, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static readonly string[] _hops = ["state", "flow"];

    /// <summary>A clock whose timers fire at once and remember how long they were asked to wait, so a retry's backoff is seen and costs no time.</summary>
    internal sealed class ImmediateTime : TimeProvider
    {
        public List<TimeSpan> Waits { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Waits.Add(dueTime);
            return base.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    internal static ContextLensesInput Input(StubLenses lenses, bool profile = true, TimeProvider? time = null) =>
        new(EngineProxy.Of(lenses), new ContextInputScopes(new ContextInputFixtures.Profiles(profile ? ContextInputFixtures.Profile() : null)), ContextInputFixtures.NoFacts(), ShippedComposition.Json, time ?? new ImmediateTime());

    private static StepInputContext Context(RepoName? repo, params string[] anchors) => new(
        BlockId.From(ContextLensesInput.Id), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["anchors"] = JsonSerializer.SerializeToElement(anchors),
            ["depth"] = JsonSerializer.SerializeToElement(2),
            ["hops"] = JsonSerializer.SerializeToElement(_hops),
        },
        new Dictionary<FactKey, JsonElement>(), repo);

    [Fact]
    public async Task Fill_reads_the_anchor_set_as_the_service_cut_to_the_instances_repo_and_returns_text_and_the_typed_view()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:a", "feature:b"), TestContext.Current.CancellationToken);

        var call = Assert.Single(lenses.Calls);
        Assert.Equal(["feature:a", "feature:b"], call.Anchors);
        Assert.Equal(2, call.Range.Depth);
        Assert.Equal("state,flow", call.Range.Hops);
        Assert.Equal(ContextCallerKind.Service, call.Scope.Caller.Kind);
        Assert.Equal("web", Assert.Single(call.Scope.Repos).Name.Value);
        Assert.Contains("lenses of 2 anchors", filled.Value.Sections["lenses"], StringComparison.Ordinal);
        Assert.Equal(2, filled.Value.Result!.Value.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(2, filled.Value.Result.Value.GetProperty("anchors").GetArrayLength());
    }

    [Fact]
    public async Task An_engine_refusal_is_an_honest_section_with_no_typed_result()
    {
        var lenses = new StubLenses(_ => ContextErrors.UnknownAnchor.With("no anchor resolved"));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:zzz"), TestContext.Current.CancellationToken);

        Assert.Contains("unavailable", filled.Value.Sections["lenses"], StringComparison.Ordinal);
        Assert.Contains("SND-CONTEXT-003", filled.Value.Sections["lenses"], StringComparison.Ordinal);
        Assert.Null(filled.Value.Result);
    }

    [Fact]
    public void The_backoff_gives_a_building_map_at_least_five_times_the_old_twenty_seconds() =>
        Assert.True(ContextLensesInput.Backoff.Aggregate(TimeSpan.Zero, static (sum, wait) => sum + wait) >= TimeSpan.FromSeconds(100));

    [Fact]
    public async Task A_refusal_that_may_pass_is_retried_with_a_bounded_backoff_and_the_view_comes_back_when_the_engine_answers()
    {
        var asked = 0;
        var lenses = new StubLenses(a => ++asked < 3 ? ContextErrors.StoreUnavailable.With("the map is building") : ContextInputFixtures.SmallSet([.. a]));
        var time = new ImmediateTime();

        var filled = await Input(lenses, time: time).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Equal(3, lenses.Calls.Count);
        Assert.Equal(ContextLensesInput.Backoff, time.Waits);
        _ = Assert.NotNull(filled.Value.Result);
    }

    [Fact]
    public async Task A_lenses_view_that_stays_unavailable_after_the_bounded_retry_is_an_honest_section_with_no_typed_result()
    {
        var lenses = new StubLenses(_ => ContextErrors.NoMap.With("the map is still building"));
        var time = new ImmediateTime();

        var filled = await Input(lenses, time: time).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextLensesInput.Backoff.Length + 1, lenses.Calls.Count);
        Assert.Equal(ContextLensesInput.Backoff.Length, time.Waits.Count);
        Assert.Contains("SND-CONTEXT-030", filled.Value.Sections["lenses"], StringComparison.Ordinal);
        Assert.Null(filled.Value.Result);
    }

    [Fact]
    public async Task A_refusal_that_cannot_pass_is_not_retried()
    {
        var lenses = new StubLenses(_ => ContextErrors.UnknownAnchor.With("no anchor resolved"));
        var time = new ImmediateTime();

        _ = await Input(lenses, time: time).Fill(Context(ContextInputFixtures.Web, "feature:zzz"), TestContext.Current.CancellationToken);

        _ = Assert.Single(lenses.Calls);
        Assert.Empty(time.Waits);
    }

    [Fact]
    public async Task A_view_far_over_256_KiB_reaches_the_Step_whole_and_the_same_on_a_refill()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.BigSet(8000, [.. a]));
        var input = Input(lenses);

        var first = await input.Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);
        var second = await input.Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        var result = first.Value.Result!.Value;
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(result.GetRawText()) > 1024 * 1024);
        Assert.Equal(8000, result.GetProperty("bands").GetProperty("product").GetProperty("neighbors").GetArrayLength());
        Assert.Equal(result.GetRawText(), second.Value.Result!.Value.GetRawText());
        Assert.Equal(first.Value.Sections["lenses"], second.Value.Sections["lenses"]);
    }

    [Fact]
    public async Task An_instance_without_a_repo_never_reaches_the_engine()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(repo: null, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("no repo", filled.Value.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repo_outside_the_profile_never_reaches_the_engine()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(RepoName.From("other"), "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("SND-CONTEXT-045", filled.Value.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_approved_profile_is_a_section_not_an_exception()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses, profile: false).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("SND-PROFILE-005", filled.Value.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_anchors_is_a_section()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("no anchors", filled.Value.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_fills_of_the_same_stored_state_at_different_clocks_give_identical_bytes()
    {
        var created = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        static LensesSetView At(IReadOnlyList<string> a, DateTimeOffset created, int days)
        {
            var view = ContextInputFixtures.SmallSet([.. a]);
            return view with
            {
                AsOf = created.AddDays(days),
                Coverage = view.Coverage with { Map = new MapCoverage(["src/"], Truncated: false, 3, 1, 0, created, days) },
                Searched = view.Searched with { Sources = [.. view.Searched.Sources.Select(s => s with { AsOf = created.AddDays(days) })] },
            };
        }

        var first = await Input(new StubLenses(a => At(a, created, 3))).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);
        var second = await Input(new StubLenses(a => At(a, created, 40))).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Equal(first.Value.Sections["lenses"], second.Value.Sections["lenses"]);
        Assert.Equal(first.Value.Result!.Value.GetRawText(), second.Value.Result!.Value.GetRawText());
        Assert.DoesNotContain("days old", first.Value.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_view_of_thousands_of_anchors_reaches_the_Step_whole_with_its_typed_result()
    {
        var lenses = new StubLenses(_ => ContextInputFixtures.SmallSet([.. Enumerable.Range(0, 20000).Select(i => "feature:f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        var text = filled.Value.Sections["lenses"];
        Assert.DoesNotContain("more lines not shown", text, StringComparison.Ordinal);
        Assert.Equal(20000, filled.Value.Result!.Value.GetProperty("anchors").GetArrayLength());
    }

    [Fact]
    public async Task An_argument_that_cannot_be_used_is_noted_in_the_section_and_an_empty_hops_list_is_the_default()
    {
        var lenses = new StubLenses(a => ContextInputFixtures.SmallSet([.. a]));
        var with = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["anchors"] = JsonSerializer.SerializeToElement(new object[] { "feature:a", 7 }),
            ["depth"] = JsonSerializer.SerializeToElement("deep"),
            ["hops"] = JsonSerializer.SerializeToElement(Array.Empty<string>()),
        };

        var filled = await Input(lenses).Fill(new StepInputContext(BlockId.From(ContextLensesInput.Id), BlockId.From("step.scope.gather"), with, new Dictionary<FactKey, JsonElement>(), ContextInputFixtures.Web), TestContext.Current.CancellationToken);

        var call = Assert.Single(lenses.Calls);
        Assert.Equal(["feature:a"], call.Anchors);
        Assert.Null(call.Range.Depth);
        Assert.Null(call.Range.Hops);
        Assert.Contains("note: 1 of 2 anchors are not text", filled.Value.Sections["lenses"], StringComparison.Ordinal);
        Assert.Contains("note: depth is not a whole number", filled.Value.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_untrusted_label_the_engine_already_fenced_is_rendered_once_not_fenced_again()
    {
        const string fenced = "<untrusted source=\"map\">Context sources</untrusted>";
        var lenses = new StubLenses(anchors =>
        {
            var view = ContextInputFixtures.SmallSet([.. anchors]);
            return view with { Anchors = [.. view.Anchors.Select(a => a with { Label = fenced, Trust = Aw.Context.Core.Results.Trust.Untrusted })] };
        });

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        var text = filled.Value.Sections["lenses"];
        Assert.Contains(fenced, text, StringComparison.Ordinal);
        Assert.DoesNotContain("&lt;untrusted", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("<untrusted").Length - 1);
    }
}
