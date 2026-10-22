namespace Aw.Sindri.Features.SetupWatch;

/// <summary>What this process has measured of setup (setup-once spec §7.5), and where in the ledger it stands. In memory only: a restart measures neither again, so a metric is never reported twice.</summary>
internal sealed class SetupMeasureLog
{
    private readonly Lock _gate = new();

    /// <summary>The 'setup.done' point: the moment and ledger sequence of the change that finished every step, or null before it.</summary>
    internal (DateTimeOffset At, long Sequence)? Done { get; private set; }

    /// <summary>Gets a value indicating whether 'setup.first-item' was measured.</summary>
    internal bool FirstItemMeasured { get; private set; }

    /// <summary>The ledger sequence the search for the first workflow start continues after.</summary>
    internal long ItemCursor { get; private set; }

    internal void MarkDone(DateTimeOffset at, long sequence)
    {
        lock (_gate)
        {
            Done = (at, sequence);
            ItemCursor = sequence;
        }
    }

    internal void AdvanceCursor(long to)
    {
        lock (_gate)
        {
            ItemCursor = Math.Max(ItemCursor, to);
        }
    }

    internal void MarkFirstItem()
    {
        lock (_gate)
        {
            FirstItemMeasured = true;
        }
    }

    /// <summary>Forgets everything, as a restart does.</summary>
    internal void Reset()
    {
        lock (_gate)
        {
            (Done, FirstItemMeasured, ItemCursor) = (null, false, 0);
        }
    }
}
