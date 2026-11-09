using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.AllowDenial;

/// <summary>
/// The Allow button (sandbox spec §3.6): checks that the run really was blocked on the target and that the fix is "allow", writes the entry into the live profile under the run's repo, records 'sandbox.allowed' with the caller's actor, and says whether the approved
/// profile already holds it or still has to be approved. Nothing is written for a refusal.
/// </summary>
/// <param name="files">The runs' counters.</param>
/// <param name="owners">The repo of each launch.</param>
/// <param name="writer">Edits the live profile.</param>
/// <param name="approved">The approved profile.</param>
/// <param name="callers">The caller: the command id and, through the event factory, the actor.</param>
/// <param name="ledger">Appends 'sandbox.allowed'.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry; its actor is the caller's.</param>
internal sealed class AllowDenialService(IDenialFiles files, ILaunchOwners owners, IProfileSandboxWriter writer, IApprovedProfile approved, ICallerContextAccessor callers, ILedgerStore ledger, HeldLease held, EventFactory events) : IService<AllowDenialRequest, AllowDenialResponse>
{
    public async ValueTask<Result<AllowDenialResponse>> Handle(AllowDenialRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("AllowDenial needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before the profile is edited
        }

        var repo = await RepoToAllowFor(request, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var written = await writer.Allow(repo.Value, request.Kind, request.Target, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var allowed = new SandboxEntryAllowed(caller.CommandId, request.Launch, RepoName.From(repo.Value), request.Kind.ToString(), written.Value.Entry);
        var appended = await ledger.Append(events.Entry(EventTypes.SandboxAllowed, allowed, $"{EventTypes.SandboxAllowed}:{caller.CommandId.Value}"), grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var inForce = latest.Value is { } snapshot && SandboxAllowances.Holds(snapshot.Profile, repo.Value, request.Kind, written.Value.Entry);
        return new AllowDenialResponse(inForce ? AllowDenialState.Allowed : AllowDenialState.NeedsApproval, repo.Value, written.Value.Entry);
    }

    // The repo of the blocked run, once the target is something that run was blocked on and that may be allowed.
    private async ValueTask<Result<string>> RepoToAllowFor(AllowDenialRequest request, CancellationToken cancellationToken)
    {
        if (request.Kind == DenialKind.Policy)
        {
            return Refused("A blocked tool can't be allowed here. Push from your own terminal, or let Sindri send it.");
        }

        var launches = await files.List(request.Launch, cancellationToken).ConfigureAwait(false);
        var entry = launches.Count == 0 ? null : launches[0].Entries.FirstOrDefault(e => e.Kind == request.Kind && string.Equals(e.Target, request.Target, StringComparison.Ordinal));
        if (entry is null)
        {
            return Refused($"Nothing was blocked at {request.Target} in that run, so there is nothing to allow.");
        }

        var decided = DenialFixes.Decide(entry);
        if (decided.Fix != DenialFix.Allow)
        {
            return Refused($"{request.Target} is never allowed: {decided.Reason}.");
        }

        var all = await owners.Read(cancellationToken).ConfigureAwait(false);
        return all.TryGetValue(request.Launch, out var owner) && owner.Repo is { } repo
            ? repo.Value
            : Refused("That run isn't tied to a repo, so there is no repo to allow it for.");
    }

    private static Error Refused(string sentence) => SindriErrors.SandboxAllowRefused.With(sentence);
}
