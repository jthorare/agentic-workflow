using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Sqlite;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.BoardLine;

public sealed class SqliteBoardStoreTests
{
    private readonly InMemoryKeyValueStore _values = new();

    private SqliteBoardStore Store() => new(_values);

    [Fact, Trait("Lane", "unit")]
    public async Task Recording_the_same_sha_again_keeps_the_version_and_unions_the_pull_requests()
    {
        _ = await Store().RecordMain("abc1234", [12], A.At(5), CancellationToken.None);
        _ = await Store().RecordMain("abc1234", [13], A.At(6), CancellationToken.None);
        _ = await Store().RecordMain("abc1234", [], A.At(7), CancellationToken.None);

        var board = (await Store().Read(CancellationToken.None)).Value;

        Assert.NotNull(board);
        Assert.Equal(1, board.Version);
        Assert.Equal([12, 13], board.MergedPrs.Order());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_different_sha_raises_the_version_and_replaces_the_pull_requests()
    {
        _ = await Store().RecordMain("abc1234", [12], A.At(5), CancellationToken.None);
        _ = await Store().RecordMain("fed9876", [13], A.At(6), CancellationToken.None);

        var board = (await Store().Read(CancellationToken.None)).Value;

        Assert.NotNull(board);
        Assert.Equal((2, "fed9876"), (board.Version, board.MainSha));
        Assert.Equal([13], board.MergedPrs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bad_entry_in_the_stored_pull_request_list_is_skipped()
    {
        _ = await _values.Set(KvKeys.BoardMain, "3|abc1234|1000|1,x,2,,-4", CancellationToken.None);

        var result = await Store().Read(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal([1, 2], result.Value.MergedPrs);
        Assert.Equal(3, result.Value.Version);
    }
}
