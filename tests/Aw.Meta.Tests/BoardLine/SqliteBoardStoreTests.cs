using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
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

    [Fact, Trait("Lane", "unit")]
    public async Task Two_writers_that_read_before_either_writes_still_raise_the_version_twice()
    {
        var racing = new RacingKeyValueStore(_values);
        var first = new SqliteBoardStore(racing);
        var second = new SqliteBoardStore(racing);

        var results = await Task.WhenAll(
            first.RecordMain("aaa1111", [1], A.At(5), CancellationToken.None).AsTask(),
            second.RecordMain("bbb2222", [2], A.At(6), CancellationToken.None).AsTask());
        var board = (await Store().Read(CancellationToken.None)).Value;

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.NotNull(board);
        Assert.Equal(2, board.Version);
        Assert.Equal(board.MainSha == "aaa1111" ? [1] : [2], board.MergedPrs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_writers_of_the_same_sha_that_interleave_end_on_one_version_with_both_pull_requests()
    {
        _ = await Store().RecordMain("old0000", [9], A.At(1), CancellationToken.None);
        var racing = new RacingKeyValueStore(_values);
        var first = new SqliteBoardStore(racing);
        var second = new SqliteBoardStore(racing);

        var results = await Task.WhenAll(
            first.RecordMain("new1111", [12], A.At(5), CancellationToken.None).AsTask(),
            second.RecordMain("new1111", [13], A.At(6), CancellationToken.None).AsTask());
        var board = (await Store().Read(CancellationToken.None)).Value;

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.NotNull(board);
        Assert.Equal((2, "new1111"), (board.Version, board.MainSha));
        Assert.Equal([12, 13], board.MergedPrs.Order());
    }

    // Holds the first two reads of the board key until both have arrived, so both writers see the same row before either writes.
    private sealed class RacingKeyValueStore(IKeyValueStore inner) : IKeyValueStore
    {
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;

        public async ValueTask<Result<string?>> Get(string key, CancellationToken cancellationToken)
        {
            var value = await inner.Get(key, cancellationToken);
            var read = key == KvKeys.BoardMain ? Interlocked.Increment(ref _reads) : int.MaxValue;
            if (read <= 2)
            {
                if (read == 2)
                {
                    _bothRead.SetResult();
                }

                await _bothRead.Task;
            }

            return value;
        }

        public ValueTask<Result<Unit>> Set(string key, string value, CancellationToken cancellationToken) =>
            inner.Set(key, value, cancellationToken);

        public ValueTask<Result<bool>> SetIf(string key, string? expected, string value, CancellationToken cancellationToken) =>
            inner.SetIf(key, expected, value, cancellationToken);
    }
}
