using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Memory;
using Aw.Sindri.Testing.Memory;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Memory;

/// <summary>#2505: a fact written again by another caller keeps its first writer and also records who refreshed it, on a real ledger.db.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteMemoryRefreshWriterTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly TempHome _home = new();
    private readonly LedgerDatabase _database;

    public SqliteMemoryRefreshWriterTests()
    {
        _database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    [Fact]
    public async Task A_refresh_by_another_caller_records_that_caller_and_keeps_the_first_writer()
    {
        var store = new SqliteMemoryStore(_database);
        _ = await store.Create(MemorySamples.Info(), Token);
        var later = KnownInstants.Epoch.AddHours(1);
        _ = await store.AddOrRefresh(MemorySamples.Fact("a", "alpha", tags: ["ops"], vector: [1f, 0.2f, 0f, 0f]) with { WrittenBy = "session:launch:A" }, Token);

        var second = await store.AddOrRefresh(MemorySamples.Fact("b", "alpha again", tags: ["ops"], vector: [1f, 0.25f, 0f, 0f]) with { RefreshedAt = later, WrittenBy = "session:launch:B" }, Token);

        Assert.True(second.Value.Refreshed);
        var row = Assert.Single((await store.Facts(MemorySamples.Name(), Token)).Value);
        Assert.Equal("session:launch:A", row.WrittenBy);
        Assert.Equal("session:launch:B", row.RefreshedBy);
    }
}
