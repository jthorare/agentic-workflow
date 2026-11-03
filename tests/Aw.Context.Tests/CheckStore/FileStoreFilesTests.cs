using Aw.Context.Infrastructure.Store;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.CheckStore;

[Trait("Lane", "integration")]
public sealed class FileStoreFilesTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private FileStoreFiles Files() => new(new ContextStoreSettings(_home.Root));

    private static void Sparse(string path, long length)
    {
        using var file = File.Create(path);
        file.SetLength(length);
    }

    [Fact]
    public void No_database_file_is_no_sizes()
    {
        var sizes = Files().Sizes();

        Assert.True(sizes.IsSuccess);
        Assert.Null(sizes.Value);
    }

    [Fact]
    public void A_database_without_a_log_has_a_log_of_0_bytes()
    {
        Sparse(Path.Combine(_home.Root, "context.db"), 4096);

        var sizes = Files().Sizes();

        Assert.Equal((4096L, 0L), (sizes.Value!.DatabaseBytes, sizes.Value.WalBytes));
    }

    [Fact]
    public void A_database_with_a_log_reports_both_sizes()
    {
        Sparse(Path.Combine(_home.Root, "context.db"), 2_500_000);
        Sparse(Path.Combine(_home.Root, "context.db-wal"), 14_000_000);

        var sizes = Files().Sizes();

        Assert.Equal((2_500_000L, 14_000_000L), (sizes.Value!.DatabaseBytes, sizes.Value.WalBytes));
    }
}
