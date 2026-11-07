using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// Lists the teams a pasted read key can see, so the owner can tick the ones to read. Owner only. The key lives in memory for this one call: Linear's refusal and silence are worded without it
/// (SND-LINEAR-004, SND-LINEAR-010), and nothing is stored.
/// </summary>
/// <param name="checks">Asks Linear.</param>
/// <param name="callers">The current caller.</param>
internal sealed class ListLinearTeamsService(ILinearKeyChecks checks, ICallerContextAccessor callers) : IService<ListLinearTeamsRequest, ListLinearTeamsResponse>
{
    public async ValueTask<Result<ListLinearTeamsResponse>> Handle(ListLinearTeamsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!LinearConnectionRules.IsOwner(callers))
        {
            return LinearConnectionRules.Refused(LinearConnectionRules.ChangeRefusal);
        }

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return SindriErrors.InvalidInput.With("paste a Linear API key (fix: make one at linear.app/settings/api)");
        }

        var teams = await checks.Teams(request.Key.Trim(), cancellationToken).ConfigureAwait(false);
        return teams.IsError ? LinearConnectionRules.Linear(teams.Error, write: false) : new ListLinearTeamsResponse(teams.Value);
    }
}
