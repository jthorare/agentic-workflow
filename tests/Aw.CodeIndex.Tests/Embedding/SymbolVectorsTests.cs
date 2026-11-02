using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Build;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class SymbolVectorsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BuildHarness> Built(FakeEmbedder? embedder = null, CodeIndexSettings? settings = null)
    {
        var harness = new BuildHarness { Embedder = embedder ?? new FakeEmbedder(), Settings = settings ?? CodeIndexSettings.Default };
        var symbols = Enumerable.Range(0, 3).Select(i => Parsed.Method("A.M" + i.ToString("D4", CultureInfo.InvariantCulture), 2 + (i * 3)) with { Body = "{ Work(" + i.ToString("D4", CultureInfo.InvariantCulture) + "); }" }).ToList();
        _ = harness.Parser.With("src/A.cs", new ParseOutcome(symbols));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");
        _ = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);
        return harness;
    }

    private static SymbolVectorsQuery Ask(params string[] paths) => new(paths);

    [Fact]
    public async Task The_stored_vectors_of_the_asked_files_come_back_with_the_model()
    {
        var harness = await Built();

        var view = (await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask("src/A.cs"), Token)).Value;

        Assert.Equal(["M0000", "M0001", "M0002"], view.Vectors.Select(v => v.Name), StringComparer.Ordinal);
        Assert.All(view.Vectors, v => Assert.Equal("src/A.cs", v.Path));
        Assert.All(view.Vectors, v => Assert.False(v.Vector.IsEmpty));
        Assert.Equal(harness.Embedder.ModelFor(CodeIndexSettings.Default.Embeddings), view.Model);
        Assert.Empty(view.Missing);
        Assert.Equal(LayerName.Embeddings, view.Stamp.Layer.Layer);
    }

    [Fact]
    public async Task A_path_with_no_embedded_symbols_is_missing()
    {
        var harness = await Built();

        var view = (await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask("src/A.cs", "src/Nope.cs", "src/Nope.cs"), Token)).Value;

        Assert.Equal(3, view.Vectors.Count);
        Assert.Equal(["src/Nope.cs"], view.Missing);
    }

    [Fact]
    public async Task The_read_embeds_nothing()
    {
        var harness = await Built();
        var batches = harness.Embedder.Batches.Count;

        _ = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask("src/A.cs"), Token);

        Assert.Equal(batches, harness.Embedder.Batches.Count);
    }

    [Fact]
    public async Task More_paths_than_the_cap_are_a_bad_query()
    {
        var harness = await Built();

        var over = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, new SymbolVectorsQuery([.. Enumerable.Range(0, CodeIndexRules.MaxSymbolVectorPaths + 1).Select(i => "src/F" + i.ToString(CultureInfo.InvariantCulture) + ".cs")]), Token);
        var at = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, new SymbolVectorsQuery([.. Enumerable.Range(0, CodeIndexRules.MaxSymbolVectorPaths).Select(i => "src/F" + i.ToString(CultureInfo.InvariantCulture) + ".cs")]), Token);

        Assert.Equal(CodeIndexErrors.QueryInvalid.Code, over.Error.Code);
        Assert.Equal(CodeIndexRules.MaxSymbolVectorPaths, at.Value.Missing.Count);
    }

    [Fact]
    public async Task No_paths_or_a_blank_path_is_a_bad_query()
    {
        var harness = await Built();

        var none = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask(), Token);
        var blank = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask("src/A.cs", " "), Token);

        Assert.Equal(CodeIndexErrors.QueryInvalid.Code, none.Error.Code);
        Assert.Equal(CodeIndexErrors.QueryInvalid.Code, blank.Error.Code);
    }

    [Fact]
    public async Task A_repo_with_no_index_or_outside_the_scope_answers_index_not_built()
    {
        var empty = new BuildHarness();
        var built = await Built();
        var elsewhere = new CodeIndexScope([new CodeIndexRepo(RepoName("web"), "/work/web", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Session, RepoName("web")));

        var none = await empty.Facade.SymbolVectors(empty.Scope, BuildHarness.Name, Ask("src/A.cs"), Token);
        var outside = await built.Facade.SymbolVectors(elsewhere, BuildHarness.Name, Ask("src/A.cs"), Token);

        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, none.Error.Code);
        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, outside.Error.Code);
    }

    [Fact]
    public async Task A_disabled_layer_answers_index_not_built()
    {
        var off = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: false, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null) };
        var harness = await Built(settings: off);

        var answer = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask("src/A.cs"), Token);

        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, answer.Error.Code);
    }

    [Fact]
    public async Task An_unavailable_layer_answers_with_its_own_code()
    {
        var harness = await Built(embedder: new FakeEmbedder { FailProbe = CodeIndexErrors.PinMismatch.With("digest differs") });

        var answer = await harness.Facade.SymbolVectors(harness.Scope, BuildHarness.Name, Ask("src/A.cs"), Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, answer.Error.Code);
    }

    private static Core.Values.RepoName RepoName(string name) => Core.Values.RepoName.From(name);
}
