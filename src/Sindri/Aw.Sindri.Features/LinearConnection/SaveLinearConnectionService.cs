using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// Saves a pasted Linear key (Linear hub spec 2026-11-04 §3). Owner only. A read key is checked against Linear first (every ticked team must be readable) and stored with its teams; a write key must differ from the read key,
/// is checked against Linear, and goes to the keychain only. Then the profile is approved when this was the only change waiting. Nothing is stored when a check fails, and the key is never in an answer.
/// </summary>
/// <param name="checks">Asks Linear.</param>
/// <param name="store">Keeps the key and its pointer.</param>
/// <param name="approval">Approves the profile when the key is the only waiting change.</param>
/// <param name="live">The live profile, for its hash.</param>
/// <param name="callers">The current caller.</param>
/// <param name="log">Keeps the result and time of the check.</param>
/// <param name="clock">The clock.</param>
internal sealed class SaveLinearConnectionService(ILinearKeyChecks checks, ILinearConnectionStore store, ILinearConnectionApproval approval, IProfileSource live, ICallerContextAccessor callers, ILinearCheckStore log, TimeProvider clock)
    : IService<SaveLinearConnectionRequest, SaveLinearConnectionResponse>
{
    public async ValueTask<Result<SaveLinearConnectionResponse>> Handle(SaveLinearConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!LinearConnectionRules.IsOwner(callers))
        {
            return LinearConnectionRules.Refused(LinearConnectionRules.ChangeRefusal);
        }

        var key = request.Key?.Trim() ?? string.Empty;
        if (key.Length == 0 || key.Any(static c => char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return SindriErrors.InvalidInput.With("paste only the Linear API key, with no spaces (fix: copy it again from linear.app/settings/api)");
        }

        var saved = request.Kind switch
        {
            "read" => await SaveRead(key, request.Teams ?? [], cancellationToken).ConfigureAwait(false),
            "write" => await SaveWrite(key, cancellationToken).ConfigureAwait(false),
            _ => SindriErrors.InvalidInput.With("kind is read or write"),
        };
        return saved.IsError ? saved.Error : await Settled(saved.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<LinearSavedWriteView?>> SaveRead(string key, IReadOnlyList<string> teams, CancellationToken cancellationToken)
    {
        if (teams.Count == 0)
        {
            return SindriErrors.LinearTeamNotPicked.With(LinearConnectionRules.NoTeams);
        }

        var readable = await checks.Teams(key, cancellationToken).ConfigureAwait(false);
        if (readable.IsError)
        {
            return LinearConnectionRules.Linear(readable.Error, write: false);
        }

        var unreadable = teams.Where(t => !readable.Value.Any(r => string.Equals(r.Key, t, StringComparison.Ordinal))).ToList();
        if (unreadable.Count > 0)
        {
            return SindriErrors.InvalidInput.With($"this key can't read {string.Join(", ", unreadable)} (fix: untick them, or paste a key from an account that belongs to them)");
        }

        var stored = await store.SaveRead(key, teams, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        _ = await log.RecordRead(new LinearStoredCheck(stored.Value, "resolves", clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);   // why: the key is saved by now, so a check that can't be kept only leaves the page at 'unchecked'
        return (LinearSavedWriteView?)null;
    }

    private async ValueTask<Result<LinearSavedWriteView?>> SaveWrite(string key, CancellationToken cancellationToken)
    {
        if (await store.IsReadKey(key, cancellationToken).ConfigureAwait(false))
        {
            return SindriErrors.LinearWriteKeySameAsRead.With(LinearConnectionRules.SameAsRead);
        }

        var scope = await checks.WriteScope(key, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return LinearConnectionRules.Linear(scope.Error, write: true);
        }

        var stored = await store.SaveWrite(key, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var state = scope.Value == LinearKeyScope.Writable ? "resolves" : "scopeUnknown";
        _ = await log.RecordWrite(new LinearStoredCheck(stored.Value, state, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return new LinearSavedWriteView(state);
    }

    private async ValueTask<Result<SaveLinearConnectionResponse>> Settled(LinearSavedWriteView? write, CancellationToken cancellationToken)
    {
        var approved = await approval.Settle(cancellationToken).ConfigureAwait(false);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || loaded.IsError)
        {
            return approved.IsError ? approved.Error : loaded.Error;
        }

        return new SaveLinearConnectionResponse(Saved: true, approved.Value, loaded.Value.Hash.Value, write);
    }
}
