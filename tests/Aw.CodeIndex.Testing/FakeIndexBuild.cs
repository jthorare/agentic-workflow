using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A build over a private copy of the fake index; <see cref="Publish"/> swaps it in, disposing without it discards it.</summary>
internal sealed class FakeIndexBuild(FakeIndexData data, IndexHeader? previous, bool incremental, Action<FakeIndexData> publish) : IIndexBuild
{
    public bool Incremental { get; } = incremental;

    public IndexHeader? Previous { get; } = previous;

    public ValueTask<Result<IReadOnlyList<StoredFile>>> StoredFiles(CancellationToken cancellationToken)
        => Ok<IReadOnlyList<StoredFile>>([.. data.Files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => new StoredFile(f.Path, f.Blob, f.Parsed))]);

    public ValueTask<Result<Unit>> WriteFiles(IReadOnlyList<FileRecord> files, CancellationToken cancellationToken)
    {
        data.Detach(files.Select(f => f.Path));
        foreach (var file in files)
        {
            data.Files[file.Path] = file;
        }

        return Done();
    }

    public ValueTask<Result<Unit>> RemoveFiles(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        data.Detach(paths);
        foreach (var path in paths)
        {
            _ = data.Files.Remove(path);
        }

        return Done();
    }

    public ValueTask<Result<Unit>> ReplaceDeps(IReadOnlyList<DepRecord> deps, CancellationToken cancellationToken)
    {
        data.Deps.Clear();
        data.Deps.AddRange(deps);
        return Done();
    }

    public ValueTask<Result<IReadOnlyList<ResolutionSymbol>>> ReadSymbols(CancellationToken cancellationToken)
        => Ok<IReadOnlyList<ResolutionSymbol>>([.. data.Symbols.Select(x => new ResolutionSymbol(x.Symbol.Ref, x.File.Path, x.Symbol.Name, x.Symbol.Qualified, x.Symbol.Kind, x.Symbol.Arity, x.Symbol.StartLine, x.Symbol.EndLine))]);

    public async IAsyncEnumerable<CallSiteRow> ReadCallSites([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var (_, symbol) in data.Symbols.ToList())
        {
            foreach (var site in symbol.CallSites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new CallSiteRow(symbol.Ref, site);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public ValueTask<Result<Unit>> ClearEdges(EdgeBasis basis, CancellationToken cancellationToken)
    {
        _ = data.Edges.RemoveAll(e => e.Basis == basis);
        return Done();
    }

    public ValueTask<Result<Unit>> AddEdges(IReadOnlyList<EdgeRecord> edges, CancellationToken cancellationToken)
    {
        var live = data.Symbols.Select(x => x.Symbol.Ref).ToHashSet();
        var keys = data.Edges.Select(FakeIndexData.Key).ToHashSet();
        foreach (var edge in edges.Where(e => live.Contains(e.Source)))
        {
            var stored = edge.Target is { } target && !live.Contains(target) ? edge with { Target = null } : edge;
            if (keys.Add(FakeIndexData.Key(stored)))
            {
                data.Edges.Add(stored);
            }
        }

        return Done();
    }

    public ValueTask<Result<Unit>> ReplaceGraph(GraphSnapshot graph, CancellationToken cancellationToken)
    {
        data.Graph = graph;
        return Done();
    }

    public ValueTask<Result<IReadOnlySet<string>>> FilesWithoutBodiesOrVectors(CancellationToken cancellationToken)
        => Ok<IReadOnlySet<string>>(data.Symbols.Where(x => x.Symbol.Body is null && !data.Embeddings.ContainsKey(x.Symbol.Ref)).Select(x => x.File.Path).ToHashSet(StringComparer.Ordinal));

    public ValueTask<Result<IReadOnlyList<EmbedCandidate>>> EmbedCandidates(int limit, CancellationToken cancellationToken)
        => Ok<IReadOnlyList<EmbedCandidate>>([.. data.Symbols
            .Where(x => x.Symbol.Body is not null && !data.Embeddings.ContainsKey(x.Symbol.Ref))
            .OrderBy(x => x.Symbol.Ref.Value, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new EmbedCandidate(x.Symbol.Ref, x.Symbol.Name, x.Symbol.Signature, x.Symbol.Body!))]);

    public ValueTask<Result<Unit>> WriteEmbeddings(IReadOnlyList<EmbeddingRecord> vectors, CancellationToken cancellationToken)
    {
        var live = data.Symbols.Select(x => x.Symbol.Ref).ToHashSet();
        var written = vectors.Where(v => live.Contains(v.Symbol)).ToList();
        foreach (var vector in written)
        {
            data.Embeddings[vector.Symbol] = vector;
        }

        var embedded = written.Select(v => v.Symbol).ToHashSet();
        foreach (var file in data.Files.Values.ToList())
        {
            if (file.Symbols.Any(s => embedded.Contains(s.Ref)))
            {
                data.Files[file.Path] = file with { Symbols = [.. file.Symbols.Select(s => embedded.Contains(s.Ref) ? s with { Body = null } : s)] };
            }
        }

        return Done();
    }

    public ValueTask<Result<EmbeddingProgress>> CountEmbeddings(CancellationToken cancellationToken)
        => Ok(new EmbeddingProgress(data.Embeddings.Count, data.Symbols.Count(x => x.Symbol.Body is not null || data.Embeddings.ContainsKey(x.Symbol.Ref))));

    public ValueTask<Result<Unit>> SetLayer(LayerRecord layer, CancellationToken cancellationToken)
    {
        data.Layers[layer.Layer] = layer;
        return Done();
    }

    public ValueTask<Result<IndexHeader>> Publish(IndexMeta meta, CancellationToken cancellationToken)
    {
        var counts = new IndexCounts(data.Files.Count, data.Symbols.Count(), data.Edges.Count);
        data.Header = new IndexHeader(meta with { Counts = counts, Schema = CodeIndexRules.SchemaVersion }, [.. data.Layers.Values.OrderBy(l => l.Layer)]);
        publish(data);
        return Ok(data.Header);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<Unit>> Done() => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
