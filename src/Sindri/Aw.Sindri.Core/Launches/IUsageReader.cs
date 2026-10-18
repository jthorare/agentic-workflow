namespace Aw.Sindri.Core.Launches;

/// <summary>Reads what an agent CLI's transcripts say it used (launcher entry 'usage': claude-code, codex or cursor).</summary>
public interface IUsageReader
{
    /// <summary>The reader's name as the catalog's 'usage' field spells it.</summary>
    public string Name { get; }

    /// <summary>False while nobody has checked the reader against the real CLI's files: the target is probed as limited and its spend is tokens only.</summary>
    public bool Verified { get; }

    /// <summary>What was used by the transcripts' messages stamped in [<paramref name="from"/>, <paramref name="to"/>), per model.</summary>
    /// <param name="from">The start, inclusive.</param>
    /// <param name="to">The end, exclusive.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per model; empty when nothing was used or the transcripts aren't there.</returns>
    public ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>What one session has used so far, per model.</summary>
    /// <param name="session">The agent's session id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per model; empty when the session's transcript isn't there.</returns>
    public ValueTask<IReadOnlyList<ModelTokens>> OfSession(Ids.SessionId session, CancellationToken cancellationToken);
}
