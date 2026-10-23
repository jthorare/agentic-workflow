using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
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

        public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static readonly string[] _hops = ["state", "flow"];

    internal static ContextLensesInput Input(StubLenses lenses, bool profile = true) =>
        new(EngineProxy.Of(lenses), new ContextInputScopes(new ContextInputFixtures.Profiles(profile ? ContextInputFixtures.Profile() : null)), ContextInputFixtures.NoFacts(), ShippedComposition.Json);

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
    public async Task A_huge_view_is_cut_at_whole_lines_under_the_cap_and_says_how_many_were_left_out()
    {
        var lenses = new StubLenses(_ => ContextInputFixtures.SmallSet([.. Enumerable.Range(0, 20000).Select(i => "feature:f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        var text = filled.Value.Sections["lenses"];
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) < 256 * 1024);
        Assert.Matches("\\[\\d+ more lines not shown", text);
        Assert.Null(filled.Value.Result);
        Assert.Contains("typed result is over", text, StringComparison.Ordinal);
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
