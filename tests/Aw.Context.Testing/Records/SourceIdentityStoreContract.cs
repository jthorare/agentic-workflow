using Aw.Context.Core.Records;
using Xunit;

namespace Aw.Context.Testing.Records;

/// <summary>The contract every <see cref="ISourceIdentityStore"/> honours: nothing before the first write, a write reads back whole, a second write replaces the first, and sources don't mix.</summary>
/// <typeparam name="TStore">The implementation.</typeparam>
public abstract class SourceIdentityStoreContract<TStore>
    where TStore : ISourceIdentityStore
{
    private static readonly string _owner = typeof(TStore).Name;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract TStore Create();

    /// <summary>A source never written has no identity.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_source_never_written_reads_as_null()
    {
        var store = Create();
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Read(RecordSamples.Linear, CancellationToken.None)) is null, "an unknown source reads as null");
    }

    /// <summary>A written identity reads back whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_written_identity_reads_back()
    {
        var store = Create();
        var identity = new SourceIdentity(RecordSamples.Linear, "linear:user/u1", "Joi", RecordSamples.Now);
        _ = ContractCheck.Ok(_owner, await store.Write(identity, CancellationToken.None));
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Read(RecordSamples.Linear, CancellationToken.None)) == identity, "the identity reads back whole");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Read(RecordSamples.Github, CancellationToken.None)) is null, "another source still has none");
    }

    /// <summary>A second write replaces the first: one row per source.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_write_replaces_the_first()
    {
        var store = Create();
        _ = ContractCheck.Ok(_owner, await store.Write(new SourceIdentity(RecordSamples.Linear, "linear:user/u1", "Joi", RecordSamples.Now), CancellationToken.None));
        var later = new SourceIdentity(RecordSamples.Linear, "linear:user/u2", "Thor", RecordSamples.Now.AddHours(2));
        _ = ContractCheck.Ok(_owner, await store.Write(later, CancellationToken.None));
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Read(RecordSamples.Linear, CancellationToken.None)) == later, "the later write is what reads back");
    }
}
