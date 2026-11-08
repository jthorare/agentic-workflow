using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The write gate over Context (Linear hub spec §5.1): a session never writes (SND-AUTH-002); the owner can once the write key resolves (SND-LINEAR-002 when it is not set, is refused or is the read key) and the issue's team
/// is one the profile picked (SND-LINEAR-003: the team's lookups are stored only for picked teams, as the lookups read finds). Context failing reads as that error's own code.
/// </summary>
/// <param name="scopes">The owner's scope.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="teams">The stored lookups: a team that has none is not picked.</param>
internal sealed class ContextLinearWriteGate(LinearScopes scopes, IContextLinearWrites engine, ILinearSideFacts teams) : ILinearWriteGate
{
    public async ValueTask<Error?> Refusal(string teamKey, CancellationToken cancellationToken)
    {
        if (scopes.IsSession())
        {
            return LinearWriteRefusals.Session();
        }

        var scope = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var status = await engine.WriteKeyStatus(scope.Value, cancellationToken).ConfigureAwait(false);
        if (status.IsError)
        {
            return string.Equals(status.Error.Code, ContextErrors.SessionsDisabled.Code, StringComparison.Ordinal) ? LinearWriteRefusals.Session() : status.Error;
        }

        if (status.Value is not LinearWriteKeyResolves)
        {
            return LinearWriteRefusals.NoWriteKey();
        }

        var lookups = await teams.Lookups(teamKey, cancellationToken).ConfigureAwait(false);
        return lookups.IsError ? lookups.Error : lookups.Value is null ? LinearWriteRefusals.TeamNotPicked(teamKey) : null;
    }
}
