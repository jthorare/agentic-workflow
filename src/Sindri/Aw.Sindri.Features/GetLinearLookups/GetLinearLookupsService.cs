using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.GetLinearLookups;

/// <summary>
/// A team's pickers (Linear hub spec 2026-11-04 §3, §4.2): the stored states, members, labels and cycles, owner only. A session is refused SND-AUTH-002 before anything is read, and Context's own refusal maps to the same.
/// A team never synced is a 200 with a null sync time and empty lists, not an error.
/// </summary>
/// <param name="facts">The stored Linear data.</param>
/// <param name="callers">The current caller.</param>
internal sealed class GetLinearLookupsService(ILinearSideFacts facts, ICallerContextAccessor callers) : IService<GetLinearLookupsRequest, LinearLookupsResponse>
{
    internal const string Refusal = "Linear's teams, states and members are the owner's to read; a session reads issues through context_* (fix: use the app)";

    public async ValueTask<Result<LinearLookupsResponse>> Handle(GetLinearLookupsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Class is not (CallerClass.Human or CallerClass.Service))
        {
            return SindriErrors.AuthRefused.With(Refusal);
        }

        var read = await facts.Lookups(request.Team, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return Refused(read.Error);
        }

        return read.Value is { } found ? found : await NotStored(request.Team, cancellationToken).ConfigureAwait(false);
    }

    // why: Context answers null for a team outside the picked ones; with teams picked that is the owner's mistake to fix in Settings, with none picked nothing has been stored yet
    private async ValueTask<Result<LinearLookupsResponse>> NotStored(string team, CancellationToken cancellationToken)
    {
        var picked = await facts.TeamsPicked(cancellationToken).ConfigureAwait(false);
        if (picked.IsError)
        {
            return picked.Error;
        }

        return picked.Value
            ? SindriErrors.LinearTeamNotPicked.With($"Sindri only reads teams you picked; {team} isn't one (fix: Settings, Connections, Linear, add {team})")
            : SindriErrors.LinearNothingStored.With($"Sindri has nothing stored for {team} yet");
    }

    // why: Context refuses a session with SND-CONTEXT-020; on this owner-only route that is SND-AUTH-002, as the caller check above
    private static Error Refused(Error error) =>
        string.Equals(error.Code, ContextErrors.SessionsDisabled.Code, StringComparison.Ordinal) ? SindriErrors.AuthRefused.With(Refusal) : error;
}
