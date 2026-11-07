using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The connection's facts over the live profile (not the approved one: a key just pasted shows at once, with `pending` saying it still waits for an approval). The read key is judged as doctor's
/// 'context:index-linear' row does, the write key by Context's own status, and the teams are the live profile's, named from what Context has stored. Nothing is sent to Linear.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="approvals">The approved profiles.</param>
/// <param name="engine">Checks the context settings.</param>
/// <param name="writes">Context's Linear writes, for the write key's status.</param>
/// <param name="secrets">Resolves the read key's pointer.</param>
/// <param name="side">The stored teams.</param>
/// <param name="log">The last save-time check of each key.</param>
internal sealed class ContextLinearConnectionFacts(IProfileSource live, IApprovedProfile approvals, IContextEngine engine, IContextLinearWrites writes, ISecretPointers secrets, ILinearSideFacts side, ILinearCheckStore log)
    : ILinearConnectionFacts
{
    private const string Linear = "context.sources.linear";

    public async ValueTask<Result<LinearConnectionView>> Read(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError || latest.IsError)
        {
            return loaded.IsError ? loaded.Error : latest.Error;
        }

        var profile = loaded.Value.Profile;
        ProfileRepoEntry[] entries = [.. (profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(profile.Context, entries);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var scope = new ContextScope(profile.Context, ScopeRules.Usable(entries), new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http));
        var status = await writes.WriteKeyStatus(scope, cancellationToken).ConfigureAwait(false);
        if (status.IsError)
        {
            return status.Error;
        }

        var now = TimeProvider.System.GetUtcNow();
        return new LinearConnectionView(
            Stamp(ReadState(settings.Value), now),
            Stamp(WriteState(status.Value), now),
            await Teams(settings.Value.LinearTeams, cancellationToken).ConfigureAwait(false),
            ProfileDiff.Between(latest.Value?.Profile, profile).Any(static k => k == Linear || k.StartsWith(Linear + ".", StringComparison.Ordinal)));
    }

    private static LinearKeyStateView Stamp(string state, DateTimeOffset now) => new(state, state == "notSet" ? null : now);

    private string ReadState(ContextSettings settings) =>
        settings.Linear is not { } linear || SourceTokens.Look(linear.Token, secrets) is not { } resolved ? "notSet" : resolved.IsError ? "refused" : "resolves";

    private static string WriteState(LinearWriteKeyStatus status) => status switch
    {
        LinearWriteKeyResolves => "resolves",
        LinearWriteKeySameAsReadKey => "sameAsRead",
        LinearWriteKeyRefused => "refused",
        _ => "notSet",
    };

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
