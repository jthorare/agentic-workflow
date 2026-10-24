using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A reader over a copy of a published fake index. Stub level: the real answers are the SQLite store's (CI-3) and the facade's (CI-6).</summary>
internal sealed class FakeIndexReader(FakeIndexData data) : IIndexReader
{
    public IndexHeader Header { get; } = data.Header!;

    public ValueTask<Result<SymbolPage>> FindSymbols(SymbolQuery query, CancellationToken cancellationToken)
    {
        if (query.Text.Length == 0 || query.Limit < 1)
        {
            return Fail<SymbolPage>(CodeIndexErrors.QueryInvalid);
        }

        var hits = data.Symbols
            .Where(x => Matches(query, x.File, x.Symbol))
            .OrderBy(x => x.File.Path, StringComparer.Ordinal)
            .ThenBy(x => x.Symbol.StartLine)
            .Select(x => FakeIndexData.ToHit(x.File, x.Symbol))
            .ToList();
        var limit = query.Limit;
        return Ok(new SymbolPage([.. hits.Take(limit)], hits.Count > limit));
    }

    public ValueTask<Result<IReadOnlyList<FileSymbolsEntry>>> FileSymbols(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        => Ok<IReadOnlyList<FileSymbolsEntry>>([.. paths.Select(p => new FileSymbolsEntry(p, SymbolsOf(p)))]);

    public ValueTask<Result<CallWalk>> Walk(SymbolRef symbol, CallDirection direction, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken)
    {
        if (data.Hit(symbol) is not { } subject)
        {
            return Fail<CallWalk>(CodeIndexErrors.SymbolNotFound);
        }

        var edges = new List<CallEdge>();
        var seen = new HashSet<SymbolRef> { symbol };
        List<SymbolRef> frontier = [symbol];
        var truncated = false;
        for (var level = 1; level <= (int)depth && frontier.Count > 0; level++)
        {
            var targets = Targets(frontier, seen, includeImplementations && direction == CallDirection.Callers);
            var step = data.Edges.Where(e => e.Kind == EdgeKind.Calls && Follows(e, direction, targets)).ToList();
            truncated |= step.Count > CodeIndexRules.MaxEdgesPerLevel;
            frontier = [];
            foreach (var edge in step.Take(CodeIndexRules.MaxEdgesPerLevel))
            {
                var other = direction == CallDirection.Callers ? edge.Source : edge.Target;
                edges.Add(new CallEdge(level, HitOf(other), edge.TargetName, edge.Kind, edge.Basis, edge.Confidence, edge.Line, edge.Candidates, RowCoverage(edge)));
                if (other is { } following && seen.Add(following))
                {
                    frontier.Add(following);
                }
            }
        }

        return Ok(new CallWalk(edges, Unresolved(symbol, subject.Name, direction), truncated, CallCoverage.Of(LanguageCoverage(symbol), edges)));
    }

    public ValueTask<Result<CloneCandidates>> CloneCandidates(SymbolRef symbol, CancellationToken cancellationToken)
    {
        var found = data.Symbols.Where(x => x.Symbol.Ref.Equals(symbol)).Select(x => x.Symbol).Cast<SymbolRecord?>().FirstOrDefault();
        if (found is null)
        {
            return Fail<CloneCandidates>(CodeIndexErrors.SymbolNotFound);
        }

        var others = data.Symbols.Where(x => !x.Symbol.Ref.Equals(symbol)).ToList();
        var exact = found.MinHash.IsEmpty
            ? []
            : others.Where(x => !x.Symbol.MinHash.IsEmpty && string.Equals(x.Symbol.ShapeHash, found.ShapeHash, StringComparison.Ordinal)).OrderBy(x => x.Symbol.Ref.Value, StringComparer.Ordinal).Select(x => FakeIndexData.ToHit(x.File, x.Symbol)).ToList();
        var banded = others
            .Where(x => !x.Symbol.MinHash.IsEmpty && x.Symbol.BandKeys.Intersect(found.BandKeys, StringComparer.Ordinal).Any())
            .OrderBy(x => x.Symbol.Ref.Value, StringComparer.Ordinal)
            .Select(x => new MinHashCandidate(FakeIndexData.ToHit(x.File, x.Symbol), x.Symbol.MinHash))
            .ToList();
        return Ok(new CloneCandidates(exact, found.MinHash, banded));
    }

    public ValueTask<Result<IReadOnlyList<Neighbor>>> Nearest(ReadOnlyMemory<float> vector, int k, CancellationToken cancellationToken)
        => Ok<IReadOnlyList<Neighbor>>([.. data.Embeddings.Values
            .Select(e => (e, Score: Cosine(vector.Span, e.Vector.Span)))
            .OrderByDescending(x => x.Score)
            .Take(k)
            .Where(x => data.Hit(x.e.Symbol) is not null)
            .Select(x => new Neighbor(data.Hit(x.e.Symbol)!, x.Score))]);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Callers of a base or interface member also include the callers of its implementations, when asked.
    private List<SymbolRef> Targets(List<SymbolRef> frontier, HashSet<SymbolRef> seen, bool withImplementations)
    {
        var targets = new List<SymbolRef>(frontier);
        if (withImplementations)
        {
            targets.AddRange(frontier.SelectMany(f => data.ImplementationsOf(data.Symbols.First(x => x.Symbol.Ref.Equals(f)).Symbol)).Where(seen.Add));
        }

        return targets;
    }

    private SymbolHit? HitOf(SymbolRef? symbol) => symbol is { } s ? data.Hit(s) : null;

    private Coverage LanguageCoverage(SymbolRef symbol)
        => string.Equals(data.Symbols.Where(x => x.Symbol.Ref.Equals(symbol)).Select(x => x.File.Language).FirstOrDefault(), "csharp", StringComparison.Ordinal) ? Coverage.Full : Coverage.Partial;

    private static Coverage RowCoverage(EdgeRecord edge) => edge.Basis == EdgeBasis.Syntax ? Coverage.Full : Coverage.Partial;

    private int Unresolved(SymbolRef symbol, string name, CallDirection direction)
        => data.Edges.Count(e => e.Kind == EdgeKind.Calls && e.Target is null && (direction == CallDirection.Callers
            ? string.Equals(e.TargetName, name, StringComparison.Ordinal)
            : e.Source.Equals(symbol)));

    private static bool Follows(EdgeRecord edge, CallDirection direction, List<SymbolRef> frontier)
        => direction == CallDirection.Callers ? edge.Target is { } target && frontier.Contains(target) : frontier.Contains(edge.Source);

    private List<SymbolHit> SymbolsOf(string path)
        => data.Files.TryGetValue(path, out var file) ? [.. file.Symbols.OrderBy(s => s.StartLine).Select(s => FakeIndexData.ToHit(file, s))] : [];

    private static bool Matches(SymbolQuery query, FileRecord file, SymbolRecord symbol)
        => query.Match == SymbolMatch.Prefix
            ? symbol.Name.StartsWith(query.Text, StringComparison.Ordinal)
            : string.Equals(symbol.Name, query.Text, StringComparison.Ordinal)
                || string.Equals(symbol.Ref.Value, query.Text, StringComparison.Ordinal)
                || string.Equals(file.Path, query.Text, StringComparison.Ordinal)
                || string.Equals($"{file.Path}#{symbol.Name}", query.Text, StringComparison.Ordinal);

    private static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<T>> Fail<T>(ErrorDefinition error) => ValueTask.FromResult<Result<T>>(error.With(error.Title));
}
