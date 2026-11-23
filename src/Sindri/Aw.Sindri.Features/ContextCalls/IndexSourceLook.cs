using Aw.Context.Core;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The one rule for whether a context source needs a look (issue #3430): the server decides it, and the app reads the flag, not the pull words.</summary>
internal static class IndexSourceLook
{
    /// <summary>How long a source on its first pass may go without a commit, counted on serve's run time too, before it needs a look (#3832, #4035).</summary>
    internal const int NoCommitLimitMinutes = 15;

    private static readonly TimeSpan _limit = TimeSpan.FromMinutes(NoCommitLimitMinutes);

    /// <summary>Whether this source needs a look: the row's own state first (a failed or backing-off pull); then a source on its first pass that
    /// committed nothing in the last 15 minutes once serve has run 15 minutes (the caller passes zero while index-sync is paused, #3909). A first
    /// pass that committed within the limit is normal work, however long it runs (#4035).</summary>
    /// <param name="p">The source's progress.</param>
    /// <param name="serveRunning">How long this serve has run.</param>
    /// <param name="now">The time now, for the age of the last commit.</param>
    /// <param name="noCommitLimit">The limit from <see cref="Limit"/>; null: 15 minutes.</param>
    /// <returns>The look and its words.</returns>
    internal static SourceLook For(SourceProgress p, TimeSpan serveRunning, DateTimeOffset now, TimeSpan? noCommitLimit = null)
    {
        _ = noCommitLimit;
        ArgumentNullException.ThrowIfNull(p);
        if (Flag(p))
        {
            return new(NeedsLook: true, Words: null);
        }

        return Stalled(p, serveRunning, now) is { } words ? new(NeedsLook: true, words) : new(NeedsLook: false, Words: null);
    }

    /// <summary>How long a first pass may go without a commit under this profile: 15 minutes, or twice index-sync's cadence when that is longer,
    /// since a commit lands only as a tick ends (#4035).</summary>
    /// <param name="profile">The approved profile, or null.</param>
    /// <returns>The limit.</returns>
    internal static TimeSpan Limit(SindriProfile? profile)
    {
        _ = profile;
        return _limit;
    }

    // why: a feed that never finishes a tick would otherwise read as a fresh source forever (#3832), and a long first backfill that keeps
    // committing is not a problem (#4035). The first pass is no tick ended yet or the backfill not done; LastCommit is the end of the last tick
    // that committed (a capped tick commits its pages), the nearest stored "last record committed"; only this serve's run time starts the limit
    private static string? Stalled(SourceProgress p, TimeSpan serveRunning, DateTimeOffset now)
    {
        var firstPass = p.LastSync is null || !p.Complete;
        if (!firstPass || serveRunning < _limit || (p.LastCommit is { } at && now - at < _limit))
        {
            return null;
        }

        var name = p.Source.Value;
        return p.LastCommit is null
            ? $"{name} hasn't finished a sync in {NoCommitLimitMinutes} minutes"
            : $"{name} hasn't committed a record in {NoCommitLimitMinutes} minutes";
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
    /// <param name="now">The time now, for the age of the last commit.</param>
    /// <param name="noCommitLimit">The limit from <see cref="Limit"/>; null: 15 minutes.</param>
    /// <returns>The problem, or null.</returns>
    internal static HealthProblem? Problem(SourceProgress p, TimeSpan serveRunning, DateTimeOffset now, TimeSpan? noCommitLimit = null)
    {
        ArgumentNullException.ThrowIfNull(p);
        var look = For(p, serveRunning, now, noCommitLimit);
        if (!look.NeedsLook)
        {
            return null;
        }

        var name = p.Source.Value;
        var fix = $"the {name} source needs a look";
        if (look.Words is not null)
        {
            return new HealthProblem(ContextErrors.SourceUnavailable.Code, look.Words, fix);   // the no-commit limit: no failed pull, so no code of its own
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
