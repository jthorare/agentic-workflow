using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.StartSession;

/// <summary>What a start writes once the session runs (agents spec §15.8): the 'agent.op' event keyed by the act id, and the repo and agent the form shows next time.</summary>
/// <param name="choices">Remembers the last repo and agent.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="gate">The command gate: the start runs outside it (a start takes up to 64 s) and takes it for the ledger write only.</param>
internal sealed class StartRecord(IStartChoices choices, ILedgerStore ledger, HeldLease held, EventFactory events, ICommandGate gate)
{
    /// <summary>The lease grant a ledger write needs, or null when this process does not hold the lease.</summary>
    internal LeaseGrant? Grant() => held.Current;

    internal async ValueTask Write(StartSessionRequest request, string actId, StartedEntry started, LeaseGrant grant)
    {
        await Append(request, actId, started, grant).ConfigureAwait(false);
        await choices.Remember(request.Repo.Value, request.Agent.Value, CancellationToken.None).ConfigureAwait(false);
    }

    private async ValueTask Append(StartSessionRequest request, string actId, StartedEntry started, LeaseGrant grant)
    {
        var body = $"{request.Repo.Value}|{request.Agent.Value}|{request.Prompt}|{request.Worktree}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var hold = await gate.Enter(CancellationToken.None).ConfigureAwait(false);
        if (hold is null)
        {
            return;   // another command held the gate for 5 s: the answer stays in memory, and the worktree route still finds it
        }

        var recorded = new AgentOpRecorded(AgentRowId.From("sindri-" + actId), "start", hash, started.Pane.Value, started.Worktree);
        var entry = events.Entry(EventTypes.AgentOp, recorded, AgentOpRecorded.KeyFor(ActId.From(actId)));
        _ = await ledger.Append(entry, grant, CancellationToken.None).ConfigureAwait(false);   // the session is running: a ledger that is down loses the durable copy, not the start
    }
}
