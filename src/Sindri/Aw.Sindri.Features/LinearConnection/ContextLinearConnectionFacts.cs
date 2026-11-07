using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The connection's facts over the live profile (not the approved one: a key just pasted shows at once, with `pending` saying it still waits for an approval). A read of the connection resolves no key and sends nothing to Linear: it runs on
/// every app event while Settings is open, and a resolve spawns a keychain or 1Password process. Each key's state is what the last save found (kept in <see cref="ILinearCheckStore"/>), trusted only while the profile still holds the pointer
/// that was checked; the checks that need no process (no pointer, the write pointer equals the read pointer, an env or file write pointer against a non-loopback Linear) are made on the profile at once. The teams are the live profile's,
/// named from what Context has stored.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="approvals">The approved profiles.</param>
/// <param name="engine">Checks the context settings.</param>
/// <param name="side">The stored teams.</param>
/// <param name="log">The last save-time check of each key.</param>
internal sealed class ContextLinearConnectionFacts(IProfileSource live, IApprovedProfile approvals, IContextEngine engine, ILinearSideFacts side, ILinearCheckStore log)
    : ILinearConnectionFacts
{
    private const string Linear = "context.sources.linear";
    private const string Unchecked = "unchecked";

    private static readonly LinearKeyStateView _notSet = new("notSet", CheckedAt: null);
    private static readonly LinearKeyStateView _unchecked = new(Unchecked, CheckedAt: null);

    public async ValueTask<Result<LinearConnectionView>> Read(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError || latest.IsError)
        {
            return loaded.IsError ? loaded.Error : latest.Error;
        }

        var profile = loaded.Value.Profile;
        ProfileRepoEntry[] entries = [.. (profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(profile.Context, entries);
        var stored = await log.Read(cancellationToken).ConfigureAwait(false);
        if (settings.IsError || stored.IsError)
        {
            return settings.IsError ? settings.Error : stored.Error;
        }

        return new LinearConnectionView(
            ReadState(settings.Value.Linear, stored.Value.Read),
            WriteState(settings.Value.Linear, stored.Value.Write),
            await Teams(settings.Value.LinearTeams, cancellationToken).ConfigureAwait(false),
            ProfileDiff.Between(latest.Value?.Profile, profile).Any(static k => k == Linear || k.StartsWith(Linear + ".", StringComparison.Ordinal)));
    }

    private static LinearKeyStateView ReadState(LinearSettings? linear, LinearStoredCheck? stored) =>
        linear is null ? _notSet : Checked(linear.Token, stored);

    private static LinearKeyStateView WriteState(LinearSettings? linear, LinearStoredCheck? stored)
    {
        if (linear?.WriteToken is not { } pointer)
        {
            return _notSet;
        }

        if (string.Equals(pointer, linear.Token, StringComparison.Ordinal))
        {
            return new LinearKeyStateView("sameAsRead", CheckedAt: null);
        }

        // why: an env or file pointer is readable by agent sessions, so the write key may not be one on a real Linear (the loopback fixture's is a file)
        var local = pointer.StartsWith("env:", StringComparison.Ordinal) || pointer.StartsWith("file:", StringComparison.Ordinal);
        return local && !linear.Url.IsLoopback ? new LinearKeyStateView("refused", CheckedAt: null) : Checked(pointer, stored);
    }

    // why: a check of another pointer (the profile was edited by hand since the save) says nothing about this one
    private static LinearKeyStateView Checked(string pointer, LinearStoredCheck? stored) =>
        stored is not null && string.Equals(stored.Pointer, pointer, StringComparison.Ordinal) ? new LinearKeyStateView(stored.State, stored.At) : _unchecked;

    // why: a picked team Context has not stored yet still shows, by key alone
    private async ValueTask<IReadOnlyList<LinearConnectionTeamView>> Teams(IReadOnlyList<string> picked, CancellationToken cancellationToken)
    {
        var teams = new List<LinearConnectionTeamView>(picked.Count);
        foreach (var key in picked)
        {
            var stored = await side.Lookups(key, cancellationToken).ConfigureAwait(false);
            teams.Add(stored is { IsSuccess: true, Value: { } found } ? new LinearConnectionTeamView(found.Team.Uuid, key, found.Team.Name) : new LinearConnectionTeamView(Id: null, key, Name: null));
        }

        return teams;
    }
}
