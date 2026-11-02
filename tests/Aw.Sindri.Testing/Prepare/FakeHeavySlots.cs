using Aw.Kernel;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Testing.Prepare;

/// <summary>An <see cref="IHeavySlots"/> with one slot that counts takes and releases and says how many leases are held right now.</summary>
public sealed class FakeHeavySlots : IHeavySlots
{
    /// <summary>The slots taken so far.</summary>
    public int Taken { get; private set; }

    /// <summary>The slots released so far.</summary>
    public int Released { get; private set; }

    /// <summary>The leases held right now.</summary>
    public int Held => Taken - Released;

    /// <summary>Whether every slot is busy: Take then answers with no lease.</summary>
    public bool Busy { get; set; }

    private readonly List<string> _holders = [];

    /// <summary>The holder names given to Take.</summary>
    public IReadOnlyList<string> Holders => _holders;

    /// <inheritdoc/>
    public ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        _holders.Add(holder);
        if (Busy)
        {
            return ValueTask.FromResult<Result<HeavySlotTaken>>(new HeavySlotTaken(Lease: null, Busy: "slot 1/1 held by someone"));
        }

        Taken++;
        return ValueTask.FromResult<Result<HeavySlotTaken>>(new HeavySlotTaken(new Lease(this), string.Empty));
    }

    private sealed class Lease(FakeHeavySlots owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Released++;
            return ValueTask.CompletedTask;
        }
    }
}
