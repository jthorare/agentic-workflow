using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>What the PR review page reads Context with: the approved profile's checked settings, the scope of the current caller and the audiences that caller may read.</summary>
internal static class ReviewScopes
{
    /// <summary>Reads the scope of the current caller.</summary>
    /// <param name="approvals">The approved profile.</param>
    /// <param name="callers">The current caller.</param>
    /// <param name="engine">Context's facade, for the settings check.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The settings, scope and audiences; or the error (no approved profile, bad settings, no caller).</returns>
    internal static async ValueTask<Result<Scoped>> Read(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(callers);
        ArgumentNullException.ThrowIfNull(engine);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the review pages need a caller");
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        // why: the audience rule needs the repos' names, not each repo's index globs, so the entries carry name and path only
        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var scope = new ContextScope(loaded.Profile.Context, ScopeRules.Usable(entries), Caller(callers.Current));
        var audience = await AudienceOf(settings.Value, scope, cancellationToken).ConfigureAwait(false);
        return audience.IsError ? audience.Error : new Scoped(settings.Value, scope, audience.Value);
    }

    /// <summary>What a human would read of the same scope: the audiences a session's claims are compared with, to count the docs it may not read.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="scope">The scope of the current caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The human's audiences, or the error.</returns>
    internal static async ValueTask<Result<AudienceScope>> HumanAudience(ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scope);
        var allowed = await RecordMask.Allowed(settings, scope.Repos, ContextCallerKind.Human, cancellationToken).ConfigureAwait(false);
        return allowed.IsError ? allowed.Error : new AudienceScope(allowed.Value);
    }

    // why: Context's own query reads with RecordMask, which adds a human's extras (owner:library, the Slack channels); a hand-built union
    // of each repo's audience drops them, so the owner saw no vault docs (#2176)
    private static async ValueTask<Result<AudienceScope>> AudienceOf(ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var allowed = await RecordMask.Allowed(settings, Asked(scope), scope.Caller.Kind, cancellationToken).ConfigureAwait(false);
        return allowed.IsError ? allowed.Error : new AudienceScope(allowed.Value);
    }

    // why: a session reads its own repo only (none when it has no repo in scope); anyone else reads every repo in scope
    private static IReadOnlyList<ContextRepo> Asked(ContextScope scope) =>
        scope.Caller.Kind == ContextCallerKind.Session ? [.. scope.Repos.Where(r => r.Name == scope.Caller.Repo)] : scope.Repos;

    private static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, ContextClient.Http),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };
}
