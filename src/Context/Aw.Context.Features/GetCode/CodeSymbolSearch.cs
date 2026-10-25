using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Features.GetCode;

/// <summary>
/// The code index's half of the code lens (S-9a): the callers and callees of the symbols the listed files define, asked of <see cref="ICodeSymbolSource"/> with the map version's commit as 'asOf', so rows from an index
/// at another commit say so with both commits. Bounded: the files one repo's read carries, the symbols asked about (exported first) and the rows listed are capped by <see cref="CodeRules"/>. A repo with no index, a build in
/// progress or a read that fails is a row of 'searched', never an error of the route.
/// </summary>
internal sealed class CodeSymbolSearch(ICodeSymbolSource source, IRepoReader repos)
{
    internal async ValueTask<CodeSymbolFindings> Find(CodeView view, VisibleMap map, ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var inScope = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        var allowed = inScope.IsError ? [] : inScope.Value;
        var version = map.Snapshot.Version;
        // why: the Where keeps only the files whose repo resolved, so Repo is never null in the grouping.
        var byRepo = view.Files.Select(f => (File: f, Repo: CodeLexicalSearch.RepoOf(map.Graph, f.Path, allowed))).Where(f => f.Repo is not null)
            .GroupBy(f => f.Repo!.Name.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(f => f.File.Path).ToList(), StringComparer.Ordinal);
        var targets = allowed.Where(r => byRepo.ContainsKey(r.Name.Value)).ToList();
        if (targets.Count == 0 && allowed.FirstOrDefault(r => r.Name == version.Repo) is { } mapRepo)
        {
            targets.Add(mapRepo);
        }

        var read = new SymbolRead();
        foreach (var repo in targets)
        {
            var asOf = repo.Name == version.Repo && CodeIndexReading.IsCommit(version.Commit) ? version.Commit : null;
            await Repo(read, new RepoAsk(new TrailingCodeSymbolSource(source, repos, repo), repo.Name, asOf, byRepo.GetValueOrDefault(repo.Name.Value) ?? []), cancellationToken).ConfigureAwait(false);
        }

        return read.Findings();
    }

    private static async ValueTask Repo(SymbolRead read, RepoAsk ask, CancellationToken cancellationToken)
    {
        var (source, repo, asOf, paths) = ask;
        var head = await source.Heads(repo, asOf, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            read.Failed(head.Error.Code);
            return;
        }

        read.Heads.Add(head.Value);
        if (paths.Count == 0 || !CodeIndexReading.Readable(head.Value))
        {
            return;
        }

        var files = await source.FileSymbols(repo, [.. paths.Take(CodeRules.MaxSymbolFiles)], asOf, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            read.Failed(files.Error.Code);
            return;
        }

        // Exported symbols first, then in file order, so the symbols asked about are the ones other code is most likely to call.
        var symbols = files.Value.Rows.SelectMany(r => r.Symbols).DistinctBy(s => s.Ref, StringComparer.Ordinal)
            .OrderByDescending(s => s.Exported).ThenBy(s => s.Path, StringComparer.Ordinal).ThenBy(s => s.StartLine).Take(read.Remaining).ToList();
        read.Asked += symbols.Count;
        await Edges(read, ask, symbols, cancellationToken).ConfigureAwait(false);
    }

    // The callers and callees of each symbol, one level; the first failed read ends the repo's edges.
    private static async ValueTask Edges(SymbolRead read, RepoAsk ask, List<SymbolSite> symbols, CancellationToken cancellationToken)
    {
        var (source, repo, asOf, _) = ask;
        foreach (var symbol in symbols)
        {
            var callers = await source.Callers(repo, symbol.Ref, 1, asOf, cancellationToken).ConfigureAwait(false);
            var callees = await source.Callees(repo, symbol.Ref, 1, asOf, cancellationToken).ConfigureAwait(false);
            if (callers.IsError || callees.IsError)
            {
                read.Failed(callers.IsError ? callers.Error.Code : callees.Error.Code);
                return;
            }

            read.Callers.AddRange(callers.Value.Rows);
            read.Callees.AddRange(callees.Value.Rows);
            read.UnresolvedCallers += callers.Value.Unresolved;
            read.UnresolvedCallees += callees.Value.Unresolved;
        }
    }

    /// <summary>What one repo is asked: its source (the pipeline's catching-up wrapper), name, the map's commit and the files to read symbols of.</summary>
    private sealed record RepoAsk(TrailingCodeSymbolSource Source, ContextRepoName Repo, string? AsOf, List<string> Paths);

    /// <summary>What the repos gave so far.</summary>
    private sealed class SymbolRead
    {
        internal List<CodeIndexHead> Heads { get; } = [];

        internal List<CallEdgeRow> Callers { get; } = [];

        internal List<CallEdgeRow> Callees { get; } = [];

        internal int UnresolvedCallers { get; set; }

        internal int UnresolvedCallees { get; set; }

        internal int Asked { get; set; }

        internal int Remaining => Math.Max(0, CodeRules.MaxCallSymbols - Asked);

        private string? _failure;

        internal void Failed(string code) => _failure ??= code;

        internal CodeSymbolFindings Findings()
        {
            if (_failure is { } code)
            {
                var down = CodeSymbols.Unavailable with { Reason = "index-unavailable", Code = code };
                return new CodeSymbolFindings(CodeIndexReading.FailedRow(code), down, down);
            }

            var head = Heads.Count == 0 ? CodeIndexHead.None : CodeIndexReading.Worst(Heads);
            return new CodeSymbolFindings(CodeIndexReading.Row(head), CodeIndexReading.Calls(head, Listed(Callers), UnresolvedCallers), CodeIndexReading.Calls(head, Listed(Callees), UnresolvedCallees));
        }

        // The strongest edge to each symbol first, every one.
        private static List<CallEdgeRow> Listed(List<CallEdgeRow> rows) =>
            [.. rows.GroupBy(r => r.Symbol.Ref, StringComparer.Ordinal).Select(g => g.OrderByDescending(r => r.Confidence).First())
                .OrderByDescending(r => r.Confidence).ThenBy(r => r.Symbol.Ref, StringComparer.Ordinal)];
    }
}
