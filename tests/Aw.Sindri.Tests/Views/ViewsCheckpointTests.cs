using Aw.Kernel.Testing;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// The projector's writer leaves the WAL alone when it closes (#2890): it opens and closes a connection per page, and a checkpoint on each
/// close copied the whole log into views.db and synced it, 100 times in a 100k catch-up.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ViewsCheckpointTests : IDisposable
{
    private readonly TempHome _home = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task A_page_the_projector_commits_stays_in_the_wal_after_its_connection_closes_and_reads_back()
    {
        using var views = new SqliteViews(_home.Path);
        _ = (await views.Store.Open(ViewSchema.Version, Token)).Value;

        var wal = new FileInfo(views.Path + "-wal");
        Assert.True(wal.Exists && wal.Length > 0, $"views.db-wal is {(wal.Exists ? $"{wal.Length} bytes" : "gone")} after the writer closed");
        Assert.Equal(0L, (await views.Reader.State(Token)).Value?.Cursor);
    }
}
