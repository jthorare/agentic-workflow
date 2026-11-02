using System.Globalization;
using Aw.CodeIndex.Core.Model;
using Xunit;

namespace Aw.CodeIndex.Testing;

public abstract partial class IndexStoreContract
{
    private static async Task<Core.Ports.IIndexReader> PublishVectors(Core.Ports.IIndexStore store, FileRecord[] files, params EmbeddingRecord[] vectors)
    {
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles(files, _none), "writing");
            Succeeds(await build.WriteEmbeddings(vectors, _none), "writing vectors");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        return await Reader(store);
    }

    /// <summary>The stored vectors of the asked files come back by path then line, each with its symbol, and files that were not asked about stay out.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Symbol_vectors_are_the_embedded_symbols_of_the_asked_files()
    {
        var late = SymbolOf("src/A.cs", "A.Late", line: 20);
        var early = SymbolOf("src/A.cs", "A.Early");
        var other = SymbolOf("src/B.cs", "B.Run");
        var unasked = SymbolOf("src/C.cs", "C.Run");
        var reader = await PublishVectors(Create(), [FileOf("src/A.cs", late, early), FileOf("src/B.cs", other), FileOf("src/C.cs", unasked)], new EmbeddingRecord(late.Ref, "m", new float[] { 0, 1 }), new EmbeddingRecord(early.Ref, "m", new float[] { 1, 0 }), new EmbeddingRecord(other.Ref, "m", new float[] { 1, 1 }), new EmbeddingRecord(unasked.Ref, "m", new float[] { 2, 2 }));
        await using (reader)
        {
            var found = Ok(await reader.SymbolVectors(["src/B.cs", "src/A.cs"], "m", _none), "symbol vectors");

            Expect.That(found.Select(v => v.Symbol.Value).SequenceEqual([early.Ref.Value, late.Ref.Value, other.Ref.Value], StringComparer.Ordinal), "by path, then line, asked files only");
            Expect.That(found[0].Path == "src/A.cs" && found[0].Name == "Early" && found[0].Kind == SymbolKind.Method, "the path, name and kind come with the vector");
            Expect.That(found[0].Vector.Span.SequenceEqual([1f, 0f]) && found[2].Vector.Span.SequenceEqual([1f, 1f]), "the stored vectors come back unchanged");
        }
    }

    /// <summary>Rows embedded with another model are ignored, and a path with no embedded symbol, or no file, contributes nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Symbol_vectors_ignore_other_models_and_unembedded_files()
    {
        var current = SymbolOf("src/A.cs", "A.Run");
        var stale = SymbolOf("src/A.cs", "A.Old", line: 10);
        var bare = SymbolOf("src/B.cs", "B.Run");
        var reader = await PublishVectors(Create(), [FileOf("src/A.cs", current, stale), FileOf("src/B.cs", bare), FileOf("src/Empty.cs")], new EmbeddingRecord(current.Ref, "m", new float[] { 1, 0 }), new EmbeddingRecord(stale.Ref, "other", new float[] { 0, 1 }));
        await using (reader)
        {
            var found = Ok(await reader.SymbolVectors(["src/A.cs", "src/B.cs", "src/Empty.cs", "src/Gone.cs"], "m", _none), "symbol vectors");

            Expect.That(found.Select(v => v.Symbol.Value).SequenceEqual([current.Ref.Value], StringComparer.Ordinal), "only the current model's rows, only files with embedded symbols");
            Expect.That(Ok(await reader.SymbolVectors(["src/A.cs"], "unknown", _none), "another model").Count == 0, "a model with no rows answers nothing");
        }
    }

    /// <summary>A path list longer than one SQL batch still returns every file's vectors, in path order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Symbol_vectors_span_more_paths_than_one_batch()
    {
        var paths = Enumerable.Range(0, 450).Select(i => string.Create(CultureInfo.InvariantCulture, $"src/F{i:D4}.cs")).ToArray();
        var symbols = paths.Select(p => SymbolOf(p, "T.Run")).ToArray();
        var files = paths.Zip(symbols, (p, s) => FileOf(p, s)).ToArray();
        var reader = await PublishVectors(Create(), files, [.. symbols.Select(s => new EmbeddingRecord(s.Ref, "m", new float[] { 1, 0 }))]);
        await using (reader)
        {
            var found = Ok(await reader.SymbolVectors([.. paths.Reverse()], "m", _none), "symbol vectors");

            Expect.That(found.Select(v => v.Path).SequenceEqual(paths, StringComparer.Ordinal), "every file's vector, in path order, across batches");
        }
    }
}
