using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// What the launches view, the catalog and the usage reader say about a launch's session: the item and whether the entry's pattern accepts a reported id (launcher L-9),
/// and the session's token totals when it ends (API spec §3.4).
/// </summary>
/// <param name="views">The launches view.</param>
/// <param name="catalog">The agent catalog.</param>
/// <param name="usage">The target's usage reader.</param>
public sealed class ReportedIdAdmission(ILaunchViews views, IAgentCatalog catalog, ILaunchUsage usage)
{
    internal TimeSpan PatternTimeout { get; init; } = SessionIdRules.DefaultTimeout;

    /// <summary>Looks the launch up.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="id">The reported id, or null when only the item is wanted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item (null when the view has no row yet) and whether the id is accepted (always for a launch with no row or an entry with no pattern), and whether the pattern timed out on it.</returns>
    public async ValueTask<AdmittedLaunch> Admit(LaunchId launch, SessionId? id, CancellationToken cancellationToken)
    {
        var read = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } row)
        {
            return new AdmittedLaunch(Item: null, Accepted: true);
        }

        var pattern = catalog.Resolve(row.Target)?.SessionIdPattern;
        var outcome = id is { } reported ? SessionIdCapture.Check(pattern, reported, PatternTimeout) : PatternOutcome.Matched;
        return new AdmittedLaunch(row.Item.Value, outcome == PatternOutcome.Matched, outcome == PatternOutcome.TimedOut);
    }

    /// <summary>The session's input and output tokens in all, read from the target's own transcript.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The totals; both null when the launch has no known session or its reader had nothing or could not read it.</returns>
    public async ValueTask<(long? Input, long? Output)> FinalTokens(LaunchId launch, CancellationToken cancellationToken)
    {
        var read = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { ResumableId: { } session } row)
        {
            return (null, null);
        }

        try
        {
            var tokens = await usage.OfSession(row.Target, session, cancellationToken).ConfigureAwait(false);
            if (tokens is null)
            {
                return (null, null);
            }

            var (input, output) = ModelTokens.Totals(tokens);
            return (input, output);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return (null, null);   // a transcript the reader can't read (or surprises it) ends the session without a total; the end itself still counts
        }
    }
}
