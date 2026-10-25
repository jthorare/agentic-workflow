using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Core.Symbols;

/// <summary>
/// The code symbol port of one repo, with an index that trails the map along main told apart from one that is stale (#990). When the inner source reports the index <see cref="CodeIndexState.Stale"/> and its commit is an ancestor of
/// the map's (the repo reader's merge base of the two is the index's commit), the head is <see cref="CodeIndexState.CatchingUp"/> with the files changed between the two commits as pending, and every row is restamped by its own
/// file (<see cref="CodeIndexReading.Stamp(CodeIndexHead, string)"/>). A non-ancestor, ancestry git cannot tell, a layer that is down and every other repo are passed through as the inner source answered.
/// </summary>
/// <param name="inner">The source that reads the index.</param>
/// <param name="repos">Reads the repo's history.</param>
/// <param name="checkout">The repo this reading is about: its name and checkout.</param>
public sealed class TrailingCodeSymbolSource(ICodeSymbolSource inner, IRepoReader repos, ContextRepo checkout) : ICodeSymbolSource
{
    private readonly Dictionary<string, CodeIndexHead> _trailed = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken)
    {
        var head = await inner.Heads(repo, asOf, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        return await Trail(repo, head.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken)
    {
        var answer = await inner.FindSymbols(repo, query, asOf, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer : await Restamped(repo, answer.Value, static (h, r) => RestampHit(h, r), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken)
    {
        var answer = await inner.FileSymbols(repo, paths, asOf, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer : await Restamped(repo, answer.Value, static (h, r) => RestampFile(h, r), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        Calls(repo, inner.Callers(repo, symbolRef, depth, asOf, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        Calls(repo, inner.Callees(repo, symbolRef, depth, asOf, cancellationToken), cancellationToken);

    private async ValueTask<Result<CallAnswer>> Calls(ContextRepoName repo, ValueTask<Result<CallAnswer>> asked, CancellationToken cancellationToken)
    {
        var answer = await asked.ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer;
        }

        var head = await Trail(repo, answer.Value.Head, cancellationToken).ConfigureAwait(false);
        return ReferenceEquals(head, answer.Value.Head) ? answer : new CallAnswer(head, [.. answer.Value.Rows.Select(r => RestampCall(head, r))], answer.Value.Unresolved);
    }

    private async ValueTask<Result<SymbolAnswer<T>>> Restamped<T>(ContextRepoName repo, SymbolAnswer<T> answer, Func<CodeIndexHead, T, T> restamp, CancellationToken cancellationToken)
    {
        var head = await Trail(repo, answer.Head, cancellationToken).ConfigureAwait(false);
        return ReferenceEquals(head, answer.Head) ? answer : new SymbolAnswer<T>(head, [.. answer.Rows.Select(r => restamp(head, r))]);
    }

    // The stale head of this repo's index, made catching-up when its commit is an ancestor of the map's; asked of git once for each pair of commits.
    private async ValueTask<CodeIndexHead> Trail(ContextRepoName repo, CodeIndexHead head, CancellationToken cancellationToken)
    {
        if (repo != checkout.Name || head is not { State: CodeIndexState.Stale, Layer: null, Commit: { } indexed, AsOfCommit: { } mapped })
        {
            return head;
        }

        var key = $"{indexed}..{mapped}";
        if (_trailed.TryGetValue(key, out var known))
        {
            return known;
        }

        var changes = await repos.Changes(checkout, new DiffRange(indexed, mapped), cancellationToken).ConfigureAwait(false);
        var trailed = changes.IsError ? head : CodeIndexReading.CatchingUp(head, changes.Value.MergeBase, [.. changes.Value.Files.Select(static f => f.Path).Where(p => !checkout.Denies(p))]);
        _trailed[key] = trailed;
        return trailed;
    }

    private static SymbolHit RestampHit(CodeIndexHead head, SymbolHit hit)
    {
        var (state, asOfCommit) = CodeIndexReading.Stamp(head, hit.Site.Path);
        return hit with { AsOfCommit = asOfCommit, State = state };
    }

    private static FileSymbolsRow RestampFile(CodeIndexHead head, FileSymbolsRow row)
    {
        var (state, asOfCommit) = CodeIndexReading.Stamp(head, row.Path);
        return row with { AsOfCommit = asOfCommit, State = state };
    }

    private static CallEdgeRow RestampCall(CodeIndexHead head, CallEdgeRow row)
    {
        var (state, asOfCommit) = CodeIndexReading.Stamp(head, row.Symbol.Path);
        return row with { AsOfCommit = asOfCommit, State = state };
    }
}
