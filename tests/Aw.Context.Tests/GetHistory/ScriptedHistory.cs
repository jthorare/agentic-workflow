using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Kernel;

namespace Aw.Context.Tests.GetHistory;

/// <summary>A file history that records what it was asked and answers from a script, or from an inner history when it has none.</summary>
/// <param name="inner">What answers when there is no script.</param>
/// <param name="script">Answers instead of <paramref name="inner"/>, or null.</param>
internal sealed class ScriptedHistory(IFileHistory inner, Func<ContextRepo, FileHistoryQuery, CancellationToken, ValueTask<Result<FileHistory>>>? script = null) : IFileHistory
{
    private readonly Lock _gate = new();

    public List<(ContextRepo Repo, FileHistoryQuery Query)> Calls { get; } = [];

    public ValueTask<Result<FileHistory>> Changes(ContextRepo repo, FileHistoryQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Calls.Add((repo, query));
        }

        return script is null ? inner.Changes(repo, query, cancellationToken) : script(repo, query, cancellationToken);
    }
}
