using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The team check of a Linear write (Linear hub spec §5.1): a write goes only to a team the profile picked, and the stored lookups of that team are what every id in the write is checked against (SND-LINEAR-006) before anything is sent.
/// A team Context holds nothing for is not picked (SND-LINEAR-003), whether the profile names no team at all or names others.
/// </summary>
/// <param name="facts">The stored Linear data.</param>
internal sealed class LinearTeamGate(ILinearSideFacts facts)
{
    /// <summary>Reads the team's stored lookups.</summary>
    /// <param name="team">The team's key, 'ENG'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lookups, or SND-LINEAR-003 when the team is not a picked one; a session's refusal (SND-CONTEXT-020) is SND-AUTH-002.</returns>
    internal async ValueTask<Result<LinearLookupsResponse>> Picked(string team, CancellationToken cancellationToken)
    {
        var read = await facts.Lookups(team, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LinearWriteFailures.Of(read.Error);
        }

        return read.Value is { } found
            ? found
            : SindriErrors.LinearTeamNotPicked.With($"Sindri only writes to teams you picked; {team} isn't one (fix: Settings, Connections, Linear, add {team})");
    }

    /// <summary>The refusal for an id that is not one of the team's.</summary>
    /// <param name="field">The body's field, 'assigneeId'.</param>
    /// <param name="team">The team's key.</param>
    /// <param name="what">What the id should have been, with its article: 'a state', 'a member', 'a cycle'.</param>
    /// <returns>SND-LINEAR-006.</returns>
    internal static Error NotInTeam(string field, string team, string what) =>
        SindriErrors.LinearBadValue.With($"{field} isn't {what} of {team} (fix: read the lookups again and pick {what} from them)");
}
