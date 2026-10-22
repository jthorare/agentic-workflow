using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Build;

namespace Aw.CodeIndex.Tests.Similarity;

[Trait("Lane", "unit")]
public sealed class SimilarBuildTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SymbolRef Ref(string qualified) => SymbolRef.From("api:src/A.cs#" + qualified);

    // 200 distinct-ish tokens; 'change' swaps one in the middle, which leaves most 3-grams (and so most of the MinHash) alone.
    private static List<string> Tokens(string prefix, int count = 200, int? change = null)
        => [.. Enumerable.Range(0, count).Select(i => prefix + (i == change ? "x" : string.Empty) + (i % 37).ToString(CultureInfo.InvariantCulture) + "_" + (i / 37).ToString(CultureInfo.InvariantCulture))];

    private static ParsedSymbol Method(string qualified, int line, IReadOnlyList<string> tokens, string shape)
        => Parsed.Method(qualified, line) with { Tokens = tokens, ShapeHash = shape };

    private static async Task<BuildHarness> Built()
    {
        var harness = new BuildHarness();
        _ = harness.Parser.With("src/A.cs", new ParseOutcome(
        [
            Method("A.One", 1, Tokens("a"), "shape-one"),
            Method("A.Same", 5, Tokens("a"), "shape-one"),
            Method("A.Near", 9, Tokens("a", change: 100), "shape-near"),
            Method("A.Other", 13, Tokens("z"), "shape-other"),
            Method("A.Tiny", 17, Tokens("a", count: 10), "shape-one"),
        ]));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        return harness;
    }

    private static async Task<SimilarAnswer> Similar(BuildHarness harness, string qualified)
    {
        var name = qualified[(qualified.LastIndexOf('.') + 1)..];
        var hit = (await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery(name, SymbolMatch.Exact, Repo: null), Token)).Value.Hits.Single();
        return (await harness.Facade.Similar(harness.Scope, hit.Ref, Token)).Value;
    }

    [Fact]
    public async Task A_build_records_the_clones_layer_ok()
    {
        var harness = await Built();

        var layer = (await harness.Header())!.Layers.Single(l => l.Layer == LayerName.Clones);

        Assert.Equal(LayerState.Ok, layer.State);
        Assert.Null(layer.Code);
    }

    [Fact]
    public async Task Similar_names_the_symbol_with_the_same_shape_as_an_exact_clone_and_never_the_subject()
    {
        var answer = await Similar(await Built(), "A.One");

        Assert.Equal(["Same"], answer.Exact.Select(h => h.Name), StringComparer.Ordinal);
        Assert.DoesNotContain(answer.Near, n => n.Symbol.Name == "One");
    }

    [Fact]
    public async Task Similar_finds_a_near_clone_by_its_bands_and_estimated_jaccard_and_not_an_unrelated_symbol()
    {
        var answer = await Similar(await Built(), "A.One");

        var near = Assert.Single(answer.Near);
        Assert.Equal("Near", near.Symbol.Name);
        Assert.InRange(near.Similarity, CodeIndexRules.NearCloneThreshold, 1.0);
    }

    [Fact]
    public async Task A_symbol_under_30_tokens_has_no_clone_rows_in_either_direction()
    {
        var harness = await Built();

        var tiny = await Similar(harness, "A.Tiny");
        var one = await Similar(harness, "A.One");

        Assert.Empty(tiny.Exact);
        Assert.Empty(tiny.Near);
        Assert.DoesNotContain(one.Exact, h => h.Name == "Tiny");
    }

    [Fact]
    public async Task The_answer_is_stamped_with_the_clones_layer()
    {
        var stamp = (await Similar(await Built(), "A.One")).Stamp;

        Assert.Equal(LayerName.Clones, stamp.Layer.Layer);
        Assert.Equal(LayerState.Ok, stamp.Layer.State);
        Assert.Equal(BuildHarness.First, stamp.Commit);
    }

    [Fact]
    public async Task A_ref_that_names_no_symbol_is_011_and_a_repo_with_no_index_is_013()
    {
        var harness = await Built();

        var missing = await harness.Facade.Similar(harness.Scope, Ref("A.Nothing"), Token);
        var none = await new BuildHarness().Facade.Similar(harness.Scope, Ref("A.One"), Token);

        Assert.Equal(CodeIndexErrors.SymbolNotFound.Code, missing.Error.Code);
        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, none.Error.Code);
    }

    [Fact]
    public async Task A_changed_file_rewrites_its_clone_rows_on_the_next_build()
    {
        var harness = await Built();
        _ = harness.Parser.With("src/A.cs", new ParseOutcome(
        [
            Method("A.One", 1, Tokens("a"), "shape-one"),
            Method("A.Same", 5, Tokens("q"), "shape-same-now-different"),
            Method("A.Near", 9, Tokens("a", change: 100), "shape-near"),
        ]));
        _ = harness.Snapshot.WithFile(BuildHarness.Second, "src/A.cs", "class A // edited");
        _ = await harness.Build(BuildHarness.Second, cancellationToken: Token);

        var answer = await Similar(harness, "A.One");

        Assert.Empty(answer.Exact);
        Assert.Equal(["Near"], answer.Near.Select(n => n.Symbol.Name), StringComparer.Ordinal);
    }
}
