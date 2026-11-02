using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// #1252: the 'progress-capped' golden seeds 250 'job.progress' events. One ledger write each is 250 transactions, and under load the row
/// outran its 30 s timeout. This pins the cost by number of ledger writes, not wall clock: the seed makes a handful, and still records every event.
/// </summary>
public sealed class ProgressCappedSeedCheckTests
{
    private const int MostWrites = 10;   // the profile approval and the job's start, and the seed's batch: nowhere near 250
    private const int EventsSeeded = 251;   // 'job.started' and 250 'job.progress'

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_progress_capped_seed_makes_a_handful_of_ledger_writes_not_250()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new WriteCount();
        await using var host = await JobHost.Start(ct, pump: false, more: services =>
        {
            _ = services.AddSingleton<SqliteLedger>();
            _ = services.Replace(ServiceDescriptor.Singleton<ILedgerStore>(sp => new CountingLedger(sp.GetRequiredService<SqliteLedger>(), writes)));
        });

        _ = await JobScenarios.Body(host, "jobs-id", "progress-capped", ct);

        var head = (await host.App.Services.GetRequiredService<ILedgerStore>().Head(ct)).Value;
        Assert.True(head >= EventsSeeded, string.Create(CultureInfo.InvariantCulture, $"the seed recorded only {head} events"));
        Assert.True(writes.Count <= MostWrites, string.Create(CultureInfo.InvariantCulture, $"the seed made {writes.Count} ledger writes, at most {MostWrites} expected"));
    }

    private sealed class WriteCount
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Add() => _ = Interlocked.Increment(ref _count);
    }

    private sealed class CountingLedger(ILedgerStore inner, WriteCount writes) : ILedgerStore
    {
        public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
        {
            writes.Add();
            return inner.Append(entry, grant, cancellationToken);
        }

        public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) => inner.ReadAfter(sequence, max, cancellationToken);

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);

        public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
    }
}
