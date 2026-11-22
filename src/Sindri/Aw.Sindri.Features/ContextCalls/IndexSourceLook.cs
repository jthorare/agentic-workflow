using Aw.Context.Core.Sync;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The one rule for whether a context source needs a look (issue #3430): the server decides it, and the app reads the flag, not the pull words.</summary>
internal static class IndexSourceLook
{
    /// <summary>How long serve runs before a source with no finished pass needs a look (#3832).</summary>
    internal const int FirstPassLimitMinutes = 10;

    /// <summary>Whether this source needs a look: a source with no finished pass once serve has run 10 minutes, else the row's own state.</summary>
    /// <param name="p">The source's progress.</param>
    /// <param name="serveRunning">How long this serve has run.</param>
    /// <returns>The look and its words.</returns>
    internal static SourceLook For(SourceProgress p, TimeSpan serveRunning)
    {
        ArgumentNullException.ThrowIfNull(p);

        // why: a feed that never finishes a tick would otherwise read as a fresh source forever (#3832); LastSync is the stored outcome, so it
        // counts a pass from any process, and only this serve's run time starts the limit
        return p.LastSync is null && serveRunning >= TimeSpan.FromMinutes(FirstPassLimitMinutes)
            ? new(NeedsLook: true, $"{p.Source.Value} hasn't finished a sync in {FirstPassLimitMinutes} minutes")
            : new(Flag(p), Words: null);
    }

    /// <summary>The look by the row's own state: its pull failed or backs off; it is idle, holds nothing and never committed although a pass has ended
    /// (before the first pass ends, a fresh source is not a look, #3183); or, with no pull read, it is unusable with a code. A running pull, or a pull
    /// word this rule doesn't name, is normal work until it is added here.</summary>
    private static bool Flag(SourceProgress p) => p.Pull switch
    {
        null => !p.Usable && !string.IsNullOrEmpty(p.LastCode),
        "failed" or "backoff" => true,
        "idle" => p.Records == 0 && p.LastCommit is null && p.LastSync is not null,
        _ => false,
    };
}
