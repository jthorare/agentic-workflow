using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The one mapping from Sindri to the context engine's scope (context spec §4): the approved profile's 'context:'
/// section and repos, and the current caller as the engine's caller (class, X-Sindri-Repo, transport from the actor:
/// 'human:cli' is cli, 'session:mcp' is mcp, other HTTP callers http, services service). Every surface (HTTP, CLI, MCP,
/// the scheduled refresh) goes through it, and the engine decides the session gate and repo scope (SND-CONTEXT-020,
/// 003). It takes the read-only approvals port (<see cref="IApprovedProfile"/>, ARCH-CQRS-002).
/// </summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
internal sealed class ContextScopes(IApprovedProfile approvals, ICallerContextAccessor callers)
{
    /// <summary>The scope of the call running now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or SND-PROFILE-005 when no profile is approved (or the approvals' own error).</returns>
    internal async ValueTask<Result<ContextScope>> Current(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved, so no repo is mapped (fix: sindri profile approve)");
        }

        return For(loaded.Profile, Caller(callers.Current));
    }

    /// <summary>The scope a profile and a caller make; SyncConfigured is whether the profile names the context-sync service.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="caller">The engine's caller.</param>
    /// <returns>The scope.</returns>
    internal static ContextScope For(SindriProfile profile, ContextCaller caller)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(profile.Context, ScopeRules.Usable(Entries(profile)), caller, profile.Services?.ContainsKey(ContextSyncService.ServiceId.Value) == true);
    }

    /// <summary>
    /// The profile's repos as the engine takes them: each with the globs of the profile's 'index.denyPaths' and its own (extraction
    /// adds them to the built-in secret globs and never reads a path they match) and the pins of 'index.concepts'. The context
    /// slices each map the profile this way themselves: a slice never uses another's types (AW1003), and Sindri's Core cannot name the engine's.
    /// </summary>
    /// <param name="profile">The approved profile.</param>
    /// <returns>The repo entries, in profile order.</returns>
    internal static IReadOnlyList<ProfileRepoEntry> Entries(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path, RepoDenyGlobs.For(profile, r), [.. RepoConcepts.For(profile, r).Select(c => ConceptPin.Of(c.Id is { } id ? ConceptId.From(id.Value) : null, c.Name, c.Summary, c.Features, c.Paths))], DemotePaths: RepoDemoteGlobs.For(profile, r)) { Roles = RepoRoleSets.For(profile, r) is { } sets ? [.. sets.Select(x => new RepoRole(x.Name, x.Demoted, x.Globs))] : null })];
    }

    /// <summary>The engine's caller for a Sindri caller; no caller at all is an in-process service.</summary>
    /// <param name="caller">The Sindri caller, or null.</param>
    /// <returns>The engine's caller.</returns>
    internal static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, Client(caller.Actor)),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, Client(caller.Actor)),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };

    /// <summary>
    /// The transport, from the label CallerContext.Http put after the class ('human:cli', 'session:mcp'): cli and mcp are
    /// themselves; the app (ui), the prover and plain HTTP are http.
    /// </summary>
    private static ContextClient Client(string actor) => actor[(actor.IndexOf(':', StringComparison.Ordinal) + 1)..] switch
    {
        "cli" => ContextClient.Cli,
        "mcp" => ContextClient.Mcp,
        _ => ContextClient.Http,
    };
}
