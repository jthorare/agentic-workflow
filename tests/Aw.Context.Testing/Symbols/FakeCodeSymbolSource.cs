using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Testing.Symbols;

/// <summary>An in-memory code index that answers <see cref="ICodeSymbolSource"/>; <see cref="CodeSymbolSourceContract"/> holds every source to its behavior. The as-of comparison is made at read time, so a call after <see cref="Land"/> reads fresh with nothing republished.</summary>
public sealed class FakeCodeSymbolSource : ICodeSymbolSource
{
    private readonly Dictionary<string, FakeIndexedRepo> _repos;

    /// <summary>Makes the index over some repos.</summary>
    /// <param name="repos">What the index holds; a repo not listed has no index.</param>
    public FakeCodeSymbolSource(IEnumerable<FakeIndexedRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);
        _repos = repos.ToDictionary(r => r.Repo.Value, StringComparer.Ordinal);
    }

    /// <summary>Builds the index for a commit: the repo is then at that commit and healthy.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit that landed.</param>
    public void Land(ContextRepoName repo, string commit) => _repos[repo.Value] = _repos[repo.Value] with { State = CodeIndexState.Ok, Commit = commit };

    /// <inheritdoc/>
    public ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken) => Done(HeadOf(repo, asOf));

    /// <inheritdoc/>
    public ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var head = HeadOf(repo, asOf);
        var (state, asOfCommit) = CodeIndexReading.Stamp(head);
        if (!Readable(repo, head, out var held))
        {
            return Done(new SymbolAnswer<SymbolHit>(head, []));
        }

        var found = held.Symbols.Where(s => Matches(s, query)).OrderBy(s => s.Ref, StringComparer.Ordinal).Take(Math.Min(query.Max, 200))
            .Select(s => new SymbolHit(s, head.Commit!, asOfCommit, state)).ToList();
        return Done(new SymbolAnswer<SymbolHit>(head, found));
    }

    /// <inheritdoc/>
    public ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var head = HeadOf(repo, asOf);
        var (state, asOfCommit) = CodeIndexReading.Stamp(head);
        if (!Readable(repo, head, out var held))
        {
            return Done(new SymbolAnswer<FileSymbolsRow>(head, []));
        }

        var rows = paths.Take(200).Distinct(StringComparer.Ordinal)
            .Select(p => (Path: p, Symbols: held.Symbols.Where(s => string.Equals(s.Path, p, StringComparison.Ordinal)).OrderBy(s => s.StartLine).ToList()))
            .Where(f => f.Symbols.Count > 0)
            .Select(f => new FileSymbolsRow(f.Path, f.Symbols, head.Commit!, asOfCommit, state)).ToList();
        return Done(new SymbolAnswer<FileSymbolsRow>(head, rows));
    }

    /// <inheritdoc/>
    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        Done(Walk(repo, symbolRef, depth, asOf, intoSymbol: true));

    /// <inheritdoc/>
    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        Done(Walk(repo, symbolRef, depth, asOf, intoSymbol: false));

    private static ValueTask<Result<T>> Done<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private CodeIndexHead HeadOf(ContextRepoName repo, string? asOf) =>
        _repos.TryGetValue(repo.Value, out var held)
            ? CodeIndexReading.Head(new CodeIndexHead(held.State, held.Commit, held.BuiltAt, AsOfCommit: null, held.Layer, held.Code), asOf)
            : CodeIndexHead.None with { Code = "SND-CODEIDX-013" };

    private bool Readable(ContextRepoName repo, CodeIndexHead head, out FakeIndexedRepo held)
    {
        held = _repos.GetValueOrDefault(repo.Value)!;
        return head.Commit is not null && head.State is not (CodeIndexState.Missing or CodeIndexState.Damaged or CodeIndexState.Building or CodeIndexState.NotConfigured);
    }

    private CallAnswer Walk(ContextRepoName repo, string symbolRef, int depth, string? asOf, bool intoSymbol)
    {
        var head = HeadOf(repo, asOf);
        if (!Readable(repo, head, out var held))
        {
            return new CallAnswer(head, [], 0);
        }

        var (state, asOfCommit) = CodeIndexReading.Stamp(head);
        var sites = held.Symbols.ToDictionary(s => s.Ref, StringComparer.Ordinal);
        var rows = new List<CallEdgeRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { symbolRef };
        var frontier = new List<string> { symbolRef };
        for (var level = 1; level <= Math.Clamp(depth, 1, 3) && frontier.Count > 0; level++)
        {
            var next = new List<string>();
            foreach (var call in held.Calls.Where(c => frontier.Contains(intoSymbol ? c.To : c.From, StringComparer.Ordinal)).OrderBy(c => c.From, StringComparer.Ordinal).ThenBy(c => c.To, StringComparer.Ordinal))
            {
                var other = intoSymbol ? call.From : call.To;
                if (!sites.TryGetValue(other, out var site) || !seen.Add(other))
                {
                    continue;
                }

                next.Add(other);
                rows.Add(new CallEdgeRow(site, call.Basis, call.Confidence, call.Coverage, level, head.Commit!, asOfCommit, state));
            }

            frontier = next;
        }

        return new CallAnswer(head, rows, held.Unresolved);
    }

    private static bool Matches(SymbolSite site, SymbolQuery query)
    {
        var text = query.Text;
        if (text.Contains('#', StringComparison.Ordinal) || text.Contains('/', StringComparison.Ordinal))
        {
            return string.Equals(site.Ref, text, StringComparison.Ordinal) || string.Equals(site.Path, text, StringComparison.Ordinal);
        }

        return query.Prefix ? site.Name.StartsWith(text, StringComparison.Ordinal) : string.Equals(site.Name, text, StringComparison.Ordinal);
    }
}
