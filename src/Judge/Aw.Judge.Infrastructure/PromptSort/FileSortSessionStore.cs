using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.PromptSort;

/// <summary>ISortSessionStore over 'judge/sessions/&lt;sid&gt;.sort.json'. Cutover unit 6.1 builds it.</summary>
internal sealed class FileSortSessionStore : ISortSessionStore
{
    /// <inheritdoc />
    public ValueTask<Result<SortSession>> Read(SessionId session, CancellationToken cancellationToken)
        => throw new NotSupportedException("FileSortSessionStore: cutover unit 6.1");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Write(SessionId session, SortSession state, CancellationToken cancellationToken)
        => throw new NotSupportedException("FileSortSessionStore: cutover unit 6.1");
}
