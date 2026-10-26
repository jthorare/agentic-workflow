using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>Passes every call on and keeps what was asked, so a test can say which repos, symbols and as-of commits the lens used (or that it asked nothing).</summary>
internal sealed class RecordingSymbolSource(ICodeSymbolSource inner) : ICodeSymbolSource
{
    private readonly Lock _gate = new();

    // why: the code fill asks symbols in parallel, so calls arrive on several threads
    internal List<(string Call, string Repo, string? AsOf, string? Subject)> Calls { get; } = [];

    public ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken)
    {
        Record(("heads", repo.Value, asOf, null));
        return inner.Heads(repo, asOf, cancellationToken);
    }

    public ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken)
    {
        Record(("find", repo.Value, asOf, query.Text));
        return inner.FindSymbols(repo, query, asOf, cancellationToken);
    }

    public ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken)
    {
        Record(("files", repo.Value, asOf, string.Join(',', paths)));
        return inner.FileSymbols(repo, paths, asOf, cancellationToken);
    }

    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken)
    {
        Record(("callers", repo.Value, asOf, symbolRef));
        return inner.Callers(repo, symbolRef, depth, asOf, cancellationToken);
    }

    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken)
    {
        Record(("callees", repo.Value, asOf, symbolRef));
        return inner.Callees(repo, symbolRef, depth, asOf, cancellationToken);
    }

    private void Record((string Call, string Repo, string? AsOf, string? Subject) call)
    {
        lock (_gate)
        {
            Calls.Add(call);
        }
    }
}
