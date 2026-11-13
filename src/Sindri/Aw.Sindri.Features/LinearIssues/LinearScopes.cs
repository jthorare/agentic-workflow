using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The scopes the Linear reads run under: the current caller's (a session sees only the teams mapped to its repo) and the owner's (names, the viewer, the write key: a session only decides which rows it sees).</summary>
/// <param name="approvals">The profiles.</param>
/// <param name="callers">The current caller.</param>
internal sealed class LinearScopes(IApprovedProfile approvals, ICallerContextAccessor callers)
{
    /// <summary>Builds a scope over the profile.</summary>
    /// <param name="owner">True for the owner's scope, false for the current caller's.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or the error when no profile yet.</returns>
    internal async ValueTask<Result<ContextScope>> Read(bool owner, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileMissing.With("no profile yet (fix: sindri repo add <path>)");
        }

        ProfileRepoEntry[] repos = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        return new ContextScope(loaded.Profile.Context, ScopeRules.Usable(repos), owner ? new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http) : Caller(callers.Current));
    }

    /// <summary>
    /// The scope a Linear write runs under: always the owner's (the write key is the owner's), but only once the current caller's own scope would let it in. A session is held to what its reads are:
    /// SND-CONTEXT-020 when the owner turned 'context.sessions.enabled' off, SND-CONTEXT-021 when the call names no repo, SND-CONTEXT-003 when the session's repo is not one the profile maps.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The owner's scope, or the caller's refusal, or the error when no profile yet.</returns>
    internal async ValueTask<Result<ContextScope>> Write(CancellationToken cancellationToken)
    {
        var caller = await Read(owner: false, cancellationToken).ConfigureAwait(false);
        if (caller.IsError)
        {
            return caller.Error;
        }

        if (caller.Value.Caller.Kind == ContextCallerKind.Session)
        {
            var settings = SettingsReader.Read(caller.Value.Settings);
            if (settings.IsError)
            {
                return settings.Error;
            }

            var resolved = ScopeRules.Resolve(settings.Value, caller.Value.Repos, caller.Value.Caller, caller.Value.LeftOut);
            if (resolved.IsError)
            {
                return resolved.Error;
            }
        }

        return await Read(owner: true, cancellationToken).ConfigureAwait(false);
    }

    private static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, ContextClient.Http),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };
}
