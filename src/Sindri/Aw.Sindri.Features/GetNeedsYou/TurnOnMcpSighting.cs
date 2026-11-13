namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>Keeps when the "Turn on for your repos" row was first seen, so the row (and the needs-you tag that hashes it) holds still while it stands (#2938).</summary>
internal sealed class TurnOnMcpSighting
{
    private readonly Lock _sync = new();
    private DateTimeOffset? _first;

    /// <summary>The time the row was first seen.</summary>
    /// <param name="now">The time now.</param>
    /// <returns>The stored time, or <paramref name="now"/> when none is stored.</returns>
    public DateTimeOffset Seen(DateTimeOffset now)
    {
        lock (_sync)
        {
            return _first ??= now;
        }
    }

    /// <summary>Forgets the stored time: the row went away.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _first = null;
        }
    }
}
