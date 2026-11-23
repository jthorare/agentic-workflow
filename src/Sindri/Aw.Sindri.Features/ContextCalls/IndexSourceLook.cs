using Aw.Context.Core;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The one rule for whether a context source needs a look (issue #3430): the server decides it, and the app reads the flag, not the pull words.</summary>
internal static class IndexSourceLook
{
    /// <summary>How long serve runs before a source with no finished pass needs a look (#3832).</summary>
    internal const int FirstPassLimitMinutes = 15;

    /// <summary>Whether this source needs a look: a source with no finished pass once serve has run 15 minutes (the caller passes zero while index-sync is paused, #3909), else the row's own state.</summary>
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

    /// <summary>The source's health problem (#4010): one line with its code, or null when the source needs no look. It asks <see cref="For"/>, so the
    /// status health and the index row never disagree.</summary>
    /// <param name="p">The source's progress.</param>
    /// <param name="serveRunning">How long this serve has run; zero while index-sync is paused.</param>
    /// <returns>The problem, or null.</returns>
    internal static HealthProblem? Problem(SourceProgress p, TimeSpan serveRunning)
    {
        ArgumentNullException.ThrowIfNull(p);
        var look = For(p, serveRunning);
        if (!look.NeedsLook)
        {
            return null;
        }

        var name = p.Source.Value;
        var fix = $"the {name} source needs a look";
        if (look.Words is not null)
        {
            return new HealthProblem(ContextErrors.SourceUnavailable.Code, look.Words, fix);   // the first-pass limit: no pass ended, so no code of its own
        }

        var code = p.LastError ?? p.LastCode ?? ContextErrors.SourceUnavailable.Code;   // a pass that found nothing carries no code of its own
        var failed = p.LastErrorText ?? "its last pull failed";
        var words = p.Pull switch
        {
            "backoff" => failed + ", retrying",
            "failed" => failed,
            "idle" => "a pass ended and found nothing",
            _ => "it can't be used yet",
        };
        return new HealthProblem(code, $"{name}: {words}", fix);
    }
}
