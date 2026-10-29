using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Supplies the issues a backtest could sample and their briefs (from the stored Linear records).</summary>
public interface IBriefSource
{
    /// <summary>The issues of the profile's Linear teams the source can list: refs, versions and dates only.</summary>
    /// <param name="scope">The call's scope.</param>
    /// <param name="since">The window's start, or null: a source that can filter by update time applies it before its cap (an issue is never updated before it was created, so none created since is lost).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The issues in key order and the teams whose listing was capped; SND-CONTEXT-081 when no configured source can supply them, or SND-CONTEXT-011 when the source is down.</returns>
    public ValueTask<Result<BriefListing>> Issues(BriefScope scope, DateTimeOffset? since, CancellationToken cancellationToken);

    /// <summary>One issue's brief.</summary>
    /// <param name="scope">The call's scope.</param>
    /// <param name="issue">The issue.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The brief; null when the source no longer has the record (the sample is dropped, not an error); or an error when the source is down.</returns>
    public ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken);
}
