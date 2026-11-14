using Aw.CodeIndex.Core.Model;
using Xunit;

namespace Aw.CodeIndex.Testing;

public abstract partial class IndexStoreContract
{
    private static readonly byte[] _minHash = [.. Enumerable.Range(0, 64).Select(i => (byte)i)];

    private static SymbolRecord Cloned(string file, string qualified, string shape, params string[] bands)
        => SymbolOf(file, qualified) with { ShapeHash = shape, TokenCount = 40, MinHash = _minHash, BandKeys = bands };

    /// <summary>Clone candidates are the symbols of the same shape and those sharing a band; a symbol with no MinHash has none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Clone_candidates_are_the_same_shape_and_the_shared_bands()
    {
        var store = Create();
        var subject = Cloned("src/A.cs", "A.Subject", "s1", "k1", "k2");
        var sameShape = Cloned("src/B.cs", "B.Same", "s1", "k3");
        var banded = Cloned("src/C.cs", "C.Banded", "s9", "k2");
        var tiny = SymbolOf("src/D.cs", "D.Tiny") with { ShapeHash = "s1" };
        _ = await Publish(store, [FileOf("src/A.cs", subject), FileOf("src/B.cs", sameShape), FileOf("src/C.cs", banded), FileOf("src/D.cs", tiny)]);

        var reader = await Reader(store);
        await using (reader)
        {
            var found = Ok(await reader.CloneCandidates(subject.Ref, _none), "clone candidates");
            Expect.That(Refs(found.Exact).SequenceEqual([sameShape.Ref.Value]), "exact clones share the shape hash and have a MinHash");
            Expect.That(found.SubjectMinHash.Span.SequenceEqual(_minHash), "the subject's MinHash comes back");
            Expect.That(found.Banded.Count == 1 && found.Banded[0].Symbol.Ref == banded.Ref && found.Banded[0].MinHash.Span.SequenceEqual(_minHash), "banded candidates share a band and carry their MinHash");

            var none = Ok(await reader.CloneCandidates(tiny.Ref, _none), "clone candidates of a tiny symbol");
            Expect.That(none.Exact.Count == 0 && none.Banded.Count == 0 && none.SubjectMinHash.IsEmpty, "a symbol under the token floor has no clone rows");
            Fails(await reader.CloneCandidates(SymbolOf("src/Z.cs", "Z.Nope").Ref, _none), "SND-CODEIDX-011", "clone candidates of an unknown ref");
        }
    }

    /// <summary>Embedding candidates are every symbol with a stored text, class-like ones included, and skip symbols with no body; writing a vector drops the body and counts as progress.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Embedding_candidates_shrink_as_vectors_are_written()
    {
        var store = Create();
        var run = SymbolOf("src/A.cs", "A.Run") with { Body = "run body" };
        var stop = SymbolOf("src/A.cs", "A.Stop", line: 10) with { Body = "stop body" };
        var type = SymbolOf("src/A.cs", "A", SymbolKind.Class, line: 20) with { Body = "class body" };
        var bare = SymbolOf("src/A.cs", "A.Bare", line: 30);
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", run, stop, type, bare)], _none), "writing");

            var first = Ok(await build.EmbedCandidates(10, _none), "candidates");
            Expect.That(first.Select(c => (c.Symbol.Value, c.Body)).SequenceEqual([(type.Ref.Value, "class body"), (run.Ref.Value, "run body"), (stop.Ref.Value, "stop body")]), "bodied symbols in ref order, with their text, a type among them");
            Expect.That(Ok(await build.EmbedCandidates(1, _none), "one candidate").Count == 1, "the limit caps the list");
            Expect.That(Ok(await build.CountEmbeddings(_none), "counting") == new EmbeddingProgress(0, 3), "none embedded; the three bodied symbols should be");

            Succeeds(await build.WriteEmbeddings([new EmbeddingRecord(run.Ref, "model-1", new float[] { 1, 0 })], _none), "writing a vector");

            Expect.That(Ok(await build.EmbedCandidates(10, _none), "candidates").Select(c => c.Symbol).SequenceEqual([type.Ref, stop.Ref]), "an embedded symbol is no longer a candidate");
            Expect.That(Ok(await build.CountEmbeddings(_none), "counting") == new EmbeddingProgress(1, 3), "one of three is embedded");
        }
    }

    /// <summary>A symbol with neither a stored text nor a vector can never be embedded, so it is not counted as embeddable (else the layer would stay pending forever).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Embeddable_counts_only_symbols_with_a_body_or_a_vector()
    {
        var store = Create();
        var bodied = SymbolOf("src/A.cs", "A.Run") with { Body = "run body" };
        var vectored = SymbolOf("src/A.cs", "A.Stop", line: 10) with { Body = "stop body" };
        var bare = SymbolOf("src/A.cs", "A.Bare", line: 20);
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", bodied, vectored, bare)], _none), "writing");
            Succeeds(await build.WriteEmbeddings([new EmbeddingRecord(vectored.Ref, "model-1", new float[] { 1, 0 })], _none), "writing a vector");

            Expect.That(Ok(await build.CountEmbeddings(_none), "counting") == new EmbeddingProgress(1, 2), "the symbol with no body and no vector is not embeddable");
        }
    }

    /// <summary>A refused symbol is stored as a marker (an empty vector): no longer a candidate, counted as refused and not as embedded, and it survives into the next build (#3128).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_marker_vector_is_no_candidate_counts_as_refused_and_is_carried_to_the_next_build()
    {
        var store = Create();
        var refused = SymbolOf("src/A.cs", "A.Huge") with { Body = "huge body" };
        var fine = SymbolOf("src/A.cs", "A.Fine", line: 10) with { Body = "fine body" };
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", refused, fine)], _none), "writing");
            Succeeds(await build.WriteEmbeddings([new EmbeddingRecord(refused.Ref, "m", ReadOnlyMemory<float>.Empty), new EmbeddingRecord(fine.Ref, "m", new float[] { 1, 0 })], _none), "writing a marker and a vector");

            Expect.That(Ok(await build.EmbedCandidates(10, _none), "candidates").Count == 0, "a refused symbol is not picked up again");
            Expect.That(Ok(await build.CountEmbeddings(_none), "counting") == new EmbeddingProgress(1, 2) { Refused = 1 }, "one embedded, one refused, both embeddable");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var next = await Begin(store, incremental: true);
        await using (next)
        {
            Expect.That(Ok(await next.EmbedCandidates(10, _none), "candidates").Count == 0, "the marker is carried into the next build");
            Expect.That(Ok(await next.CountEmbeddings(_none), "counting") == new EmbeddingProgress(1, 2) { Refused = 1 }, "and still counted as refused");
        }
    }

    /// <summary>A marker never reaches a read: not a nearest neighbour and not a symbol vector (#3128).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_marker_vector_is_never_returned_by_nearest_or_symbol_vectors()
    {
        var refused = SymbolOf("src/A.cs", "A.Huge");
        var fine = SymbolOf("src/A.cs", "A.Fine", line: 10);
        var reader = await PublishVectors(Create(), [FileOf("src/A.cs", refused, fine)], new EmbeddingRecord(refused.Ref, "m", ReadOnlyMemory<float>.Empty), new EmbeddingRecord(fine.Ref, "m", new float[] { 1, 0 }));
        await using (reader)
        {
            var nearest = Ok(await reader.Nearest(new float[] { 1, 0 }, 10, _none), "nearest");
            var vectors = Ok(await reader.SymbolVectors(["src/A.cs"], "m", _none), "symbol vectors");

            Expect.That(Refs(nearest.Select(n => n.Symbol)).SequenceEqual([fine.Ref.Value]), "the marker is no neighbour");
            Expect.That(vectors.Select(v => v.Symbol.Value).SequenceEqual([fine.Ref.Value], StringComparer.Ordinal), "the marker is no symbol vector");
        }
    }

    /// <summary>The nearest vectors come back best first, with their symbols, and at most k of them.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Nearest_returns_the_best_vectors_first()
    {
        var store = Create();
        var a = SymbolOf("src/A.cs", "A.Run");
        var b = SymbolOf("src/A.cs", "A.Stop", line: 10);
        var c = SymbolOf("src/A.cs", "A.Wait", line: 20);
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", a, b, c)], _none), "writing");
            Succeeds(await build.WriteEmbeddings([new EmbeddingRecord(a.Ref, "m", new float[] { 1, 0 }), new EmbeddingRecord(b.Ref, "m", new float[] { 1, 1 }), new EmbeddingRecord(c.Ref, "m", new float[] { 0, 1 })], _none), "writing vectors");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            var nearest = Ok(await reader.Nearest(new float[] { 1, 0.1f }, 2, _none), "nearest 2");

            Expect.That(Refs(nearest.Select(n => n.Symbol)).SequenceEqual([a.Ref.Value, b.Ref.Value]), "the closest two come first, closest first");
            Expect.That(nearest[0].Score > nearest[1].Score && Math.Abs(nearest[0].Score - 0.995) < 0.01, "the score is the cosine similarity");
            Expect.That(Ok(await reader.Nearest(new float[] { 1, 0 }, 0, _none), "nearest 0").Count == 0, "k of 0 asks for nothing");
        }
    }
}
