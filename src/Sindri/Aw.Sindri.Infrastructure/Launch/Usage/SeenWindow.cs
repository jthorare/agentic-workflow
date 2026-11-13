namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// The widest window an incremental usage reader was asked about, so a caller over a wider window and the read of today don't evict each other's files back to byte 0.
/// A window that starts at or after everything seen so far is a new day: the memory starts over. Not thread-safe; the reader calls it under its own gate.
/// </summary>
internal sealed class SeenWindow
{
    private DateTimeOffset? _earliest;
    private DateTimeOffset _horizon;

    /// <summary>Takes one asked window in.</summary>
    /// <param name="from">The window's start.</param>
    /// <param name="to">The window's end.</param>
    /// <returns>The earliest start to list files from: files older than it are evicted.</returns>
    public DateTimeOffset Widen(DateTimeOffset from, DateTimeOffset to)
    {
        if (_earliest is not { } earliest || from >= _horizon)
        {
            earliest = from;
            _horizon = to;
        }
        else
        {
            earliest = earliest < from ? earliest : from;
            _horizon = _horizon > to ? _horizon : to;
        }

        _earliest = earliest;
        return earliest;
    }
}
