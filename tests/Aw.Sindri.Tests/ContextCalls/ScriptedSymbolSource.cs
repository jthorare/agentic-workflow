using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// A code index that is at each repo's checkout head while <see cref="Indexed"/> is on and has no index while it is off, so a map build records (or does not record) the code index commit it read (#1704).
/// It holds no symbols: a citation finds nothing, but the head it answers with is what the draft's heads keep.
/// </summary>
internal sealed class ScriptedSymbolSource(IReadOnlyDictionary<string, string> paths) : ICodeSymbolSource
{
    /// <summary>True when the index holds each repo at the head of its checkout.</summary>
    internal bool Indexed { get; set; } = true;

    public async ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken) => await HeadOf(repo);

    public async ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken) =>
        new SymbolAnswer<SymbolHit>(await HeadOf(repo), []);

    public async ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken) =>
        new SymbolAnswer<FileSymbolsRow>(await HeadOf(repo), []);

    public async ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        new CallAnswer(await HeadOf(repo), [], 0);

    public async ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        await Callers(repo, symbolRef, depth, asOf, cancellationToken);

    private async ValueTask<CodeIndexHead> HeadOf(ContextRepoName repo) =>
        Indexed && paths.TryGetValue(repo.Value, out var path)
            ? new CodeIndexHead(CodeIndexState.Ok, await MapPublishRig.Head(path), BuiltAt: null, AsOfCommit: null, Layer: null, Code: null)
            : CodeIndexHead.None;
}
