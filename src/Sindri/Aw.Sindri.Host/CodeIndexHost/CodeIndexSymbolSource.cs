using System.Text;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Kernel;
using IndexSymbolHit = Aw.CodeIndex.Core.Facade.SymbolHit;
using IndexSymbolQuery = Aw.CodeIndex.Core.Facade.SymbolQuery;
using SymbolHit = Aw.Context.Core.Symbols.SymbolHit;
using SymbolQuery = Aw.Context.Core.Symbols.SymbolQuery;

namespace Aw.Sindri.Host.CodeIndexHost;

/// <summary>
/// Context's <see cref="ICodeSymbolSource"/> over the code index facade (code-index spec §7.2, §7.3). The block's state words (the index file's state and its graph layer) become <see cref="CodeIndexState"/>;
/// <see cref="CodeIndexReading.Head"/> then compares the index's commit with the map's ('asOf') at read time, so a mismatch reads stale with both commits and clears itself when the index for the map's commit lands.
/// A repo outside the scope (no approved profile entry, or another session's repo) is not-configured, one with no index or a damaged one is an answer with no rows, and only a failed read is an error.
/// Context names a symbol 'path#name'; the index names it '&lt;repo&gt;:path#name'.
/// </summary>
internal sealed class CodeIndexSymbolSource(ICodeIndex index, ICodeIndexScopeSource scopes) : ICodeSymbolSource
{
    private const string DamagedCode = "SND-CODEIDX-010";
    private const string NotBuiltCode = "SND-CODEIDX-013";

    private static readonly string[] _noEdges = ["SND-CODEIDX-013", "SND-CODEIDX-011"];

    public async ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken)
    {
        var read = await Read(repo, asOf, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Head;
    }

    public async ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var read = await Read(repo, asOf, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Scope is not { } scope || !CodeIndexReading.Readable(read.Value.Head))
        {
            return read.IsError ? read.Error : new SymbolAnswer<SymbolHit>(read.Value.Head, []);
        }

        var head = read.Value.Head;
        var asked = new IndexSymbolQuery(query.Text, query.Prefix ? SymbolMatch.Prefix : SymbolMatch.Exact, read.Value.Name, Math.Max(query.Max, 1));
        var found = await index.FindSymbols(scope, asked, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error.Code == NotBuiltCode ? new SymbolAnswer<SymbolHit>(head, []) : found.Error;
        }

        var (state, asOfCommit) = CodeIndexReading.Stamp(head);
        // why: the head is readable here, so it names a commit.
        return new SymbolAnswer<SymbolHit>(head, [.. found.Value.Hits.Select(h => new SymbolHit(Site(h), head.Commit!, asOfCommit, state))]);
    }

    public async ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var read = await Read(repo, asOf, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Scope is not { } scope || !CodeIndexReading.Readable(read.Value.Head) || read.Value.Name is not { } name)
        {
            return read.IsError ? read.Error : new SymbolAnswer<FileSymbolsRow>(read.Value.Head, []);
        }

        var head = read.Value.Head;
        var files = await index.FileSymbols(scope, name, [.. paths.Take(200)], cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error.Code == NotBuiltCode ? new SymbolAnswer<FileSymbolsRow>(head, []) : files.Error;
        }

        // A file the index does not know comes back with no symbols, and has no row.
        var (state, asOfCommit) = CodeIndexReading.Stamp(head);
        // why: the head is readable here, so it names a commit.
        return new SymbolAnswer<FileSymbolsRow>(head, [.. files.Value.Files.Where(static f => f.Symbols.Count > 0).Select(f => new FileSymbolsRow(f.Path, [.. f.Symbols.Select(Site)], head.Commit!, asOfCommit, state))]);
    }

    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        Calls(new CallsAsk(repo, symbolRef, depth, asOf, Callers: true), cancellationToken);

    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        Calls(new CallsAsk(repo, symbolRef, depth, asOf, Callers: false), cancellationToken);

    private async ValueTask<Result<CallAnswer>> Calls(CallsAsk ask, CancellationToken cancellationToken)
    {
        var (repo, symbolRef, depth, asOf, callers) = ask;
        ArgumentNullException.ThrowIfNull(symbolRef);
        var read = await Read(repo, asOf, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Scope is not { } scope || !CodeIndexReading.Readable(read.Value.Head))
        {
            return read.IsError ? read.Error : new CallAnswer(read.Value.Head, [], 0);
        }

        var head = read.Value.Head;
        if (!SymbolRef.TryFrom($"{repo.Value}:{symbolRef}", out var symbol))
        {
            return new CallAnswer(head, [], 0);
        }

        var levels = (CallDepth)Math.Clamp(depth, 1, 8);
        var answer = callers
            ? await index.Callers(scope, symbol, levels, includeImplementations: false, cancellationToken).ConfigureAwait(false)
            : await index.Callees(scope, symbol, levels, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            // A ref the index does not know has no edges: that is an answer, not a failed read.
            return _noEdges.Contains(answer.Error.Code) ? new CallAnswer(head, [], 0) : answer.Error;
        }

        var (state, asOfCommit) = CodeIndexReading.Stamp(head);
        // why: the Where keeps the edges that resolved to a symbol; the head is readable, so it names a commit.
        var rows = answer.Value.Edges.Where(static e => e.Other is not null)
            .Select(e => new CallEdgeRow(Site(e.Other!), Name(e.Basis), Weight(e.Confidence), Name(e.Coverage), e.Level, head.Commit!, asOfCommit, state)).ToList();
        return new CallAnswer(head, rows, answer.Value.UnresolvedCount + answer.Value.Edges.Count(static e => e.Other is null));
    }

    // The head of one repo and the scope to ask the facade in; a repo outside the scope is not configured and the scope is null.
    private async ValueTask<Result<Reading>> Read(ContextRepoName repo, string? asOf, CancellationToken cancellationToken)
    {
        if (!RepoName.TryFrom(repo.Value, out var name))
        {
            return new Reading(CodeIndexHead.None, Scope: null, Name: null);
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error.Code == "SND-PROFILE-005" ? new Reading(NotConfigured(), Scope: null, name) : scope.Error;
        }

        if (scope.Value.Repos.All(r => r.Name != name))
        {
            return new Reading(NotConfigured(), Scope: null, name);
        }

        var status = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        if (status.IsError)
        {
            return status.Error;
        }

        var row = status.Value.Repos.FirstOrDefault(r => r.Repo == name);
        return new Reading(row is null ? CodeIndexHead.None : CodeIndexReading.Head(Own(row), asOf), scope.Value, name);
    }

    private static CodeIndexHead NotConfigured() => CodeIndexHead.None with { State = CodeIndexState.NotConfigured };

    // What the block says by itself. Stale and checkout-not-on-main are the service's to know (git), not the files', so they are not said here; the as-of comparison is the caller's.
    private static CodeIndexHead Own(RepoStatus row)
    {
        var commit = row.Commit?.Value;
        if (row.State == IndexState.Damaged)
        {
            return new CodeIndexHead(CodeIndexState.Damaged, commit, row.BuiltAt, AsOfCommit: null, Layer: null, DamagedCode);
        }

        if (row.State == IndexState.Missing)
        {
            return new CodeIndexHead(CodeIndexState.Missing, Commit: null, BuiltAt: null, AsOfCommit: null, Layer: null, NotBuiltCode);
        }

        var down = row.Layers.FirstOrDefault(static l => l.Layer == LayerName.Graph && l.State == LayerState.Unavailable);
        return down is null
            ? new CodeIndexHead(CodeIndexState.Ok, commit, row.BuiltAt, AsOfCommit: null, Layer: null, Code: null)
            : new CodeIndexHead(CodeIndexState.LayerUnavailable, commit, row.BuiltAt, AsOfCommit: null, Layer: "graph", down.Code);
    }

    private static SymbolSite Site(IndexSymbolHit hit)
    {
        var text = hit.Ref.Value;
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        return new SymbolSite(colon < 0 ? text : text[(colon + 1)..], hit.Name, Name(hit.Kind), hit.File, hit.StartLine, hit.EndLine, string.IsNullOrEmpty(hit.Signature) ? null : hit.Signature, hit.Exported);
    }

    // The words the index uses on the wire: PascalCase as kebab-case ('LocalFunction' is 'local-function').
    private static string Name<T>(T value)
        where T : struct, Enum
    {
        var text = value.ToString();
        var kebab = new StringBuilder(text.Length + 2);
        foreach (var c in text)
        {
            _ = char.IsUpper(c) ? kebab.Append(kebab.Length > 0 ? "-" : string.Empty).Append(char.ToLowerInvariant(c)) : kebab.Append(c);
        }

        return kebab.ToString();
    }

    private static double Weight(Confidence confidence) => confidence switch
    {
        Confidence.Certain => 1.0,
        Confidence.Likely => 0.75,
        Confidence.Ambiguous => 0.4,
        Confidence.Unresolved => 0.0,
        _ => 0.0,
    };

    private sealed record CallsAsk(ContextRepoName Repo, string SymbolRef, int Depth, string? AsOf, bool Callers);

    private sealed record Reading(CodeIndexHead Head, CodeIndexScope? Scope, RepoName? Name);
}
