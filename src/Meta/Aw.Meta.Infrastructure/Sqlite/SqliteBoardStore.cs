using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IBoardStore over meta.db. Red stub: the lead implements it.</summary>
internal sealed class SqliteBoardStore : IBoardStore
{
    public ValueTask<Result<Unit>> RecordMain(string sha, IReadOnlyList<int> mergedPrs, DateTimeOffset at, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<BoardFacts?>> Read(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<BoardSeen?>> SeenVersion(string lane, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> MarkSeen(string lane, BoardSeen seen, CancellationToken cancellationToken) => throw new NotSupportedException();
}
