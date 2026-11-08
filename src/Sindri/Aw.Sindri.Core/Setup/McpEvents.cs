using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Setup;

/// <summary>Records 'mcp.registered' (setup-once spec §7.5) for the command in force: the repo, the port the registered address names and each provider's outcome. Key 'mcp.registered:&lt;commandId&gt;'.</summary>
/// <param name="held">The lease the control plane holds.</param>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller of the command in force.</param>
public sealed class McpEvents(HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
{
    private const string McpStep = "mcp";

    /// <summary>Appends 'mcp.registered'.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="port">The port.</param>
    /// <param name="providers">One outcome per provider.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or why it could not be recorded.</returns>
    public async ValueTask<Result<Unit>> Registered(RepoName repo, int port, IReadOnlyList<McpProviderOutcome> providers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providers);
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var key = "mcp.registered:" + caller.CommandId.Value;
        var appended = await ledger.Append(events.Entry(EventTypes.McpRegistered, new McpRegistered(caller.CommandId, repo, port, providers), key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    /// <summary>Whether consent for the 'mcp' step was recorded for <paramref name="profile"/> (setup-once spec §5.4: the profile's approval, or the owner's press of "Turn on for your repos").</summary>
    /// <param name="profile">The approved profile's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the newest 'setup.consent.given' names that profile and the 'mcp' step.</returns>
    public async ValueTask<bool> ConsentGiven(ProfileHash profile, CancellationToken cancellationToken)
    {
        var latest = await ledger.Latest(EventTypes.SetupConsentGiven, cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } recorded)
        {
            return false;
        }

        var payload = events.Payload<SetupConsentGiven>(recorded);
        return payload.IsSuccess && payload.Value.ProfileHash == profile && payload.Value.Steps.Contains(McpStep, StringComparer.Ordinal);
    }

    /// <summary>Records the owner's consent when a human is the caller and none is recorded for <paramref name="profile"/> yet (the press of "Turn on for your repos"); nothing otherwise.</summary>
    /// <param name="profile">The approved profile's hash.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or why it could not be recorded.</returns>
    public async ValueTask<Result<Unit>> ConsentOnHumanPress(ProfileHash profile, CancellationToken cancellationToken)
    {
        if (callers.Current is not { Class: CallerClass.Human } caller || await ConsentGiven(profile, cancellationToken).ConfigureAwait(false))
        {
            return Unit.Value;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var given = new SetupConsentGiven(caller.CommandId, [McpStep, "map"], profile);
        var appended = await ledger.Append(events.Entry(EventTypes.SetupConsentGiven, given, "setup.consent.given:" + profile.Value), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
