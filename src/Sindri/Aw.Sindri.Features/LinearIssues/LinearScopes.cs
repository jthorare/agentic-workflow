using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The scopes the Linear reads run under: the current caller's (a session sees only the teams mapped to its repo) and the owner's (names, the viewer, the write key: a session is refused some of them and only decides which rows it sees).</summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
internal sealed class LinearScopes(IApprovedProfile approvals, ICallerContextAccessor callers)
{
    /// <summary>Whether the current caller is an agent session.</summary>
    internal bool IsSession() => callers.Current?.Class == CallerClass.Session;

    /// <summary>Builds a scope over the approved profile.</summary>
    /// <param name="owner">True for the owner's scope, false for the current caller's.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or the error when no profile is approved.</returns>
    internal async ValueTask<Result<ContextScope>> Read(bool owner, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        ProfileRepoEntry[] repos = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        return new ContextScope(loaded.Profile.Context, ScopeRules.Usable(repos), owner ? new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http) : Caller(callers.Current));
    }

    private static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, ContextClient.Http),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };
}
