using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The one mapping from Sindri to the context engine's scope (context spec §4): the profile's 'context:'
/// section and repos, and the current caller as the engine's caller (class, X-Sindri-Repo, transport from the actor:
/// 'human:cli' is cli, 'session:mcp' is mcp, other HTTP callers http, services service). Every surface (HTTP, CLI, MCP,
/// the scheduled refresh) goes through it, and the engine decides the session gate and repo scope (SND-CONTEXT-020,
/// 003). It takes the read-only approvals port (<see cref="IApprovedProfile"/>, ARCH-CQRS-002).
/// </summary>
/// <param name="approvals">The profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="checkouts">Which profile repos are git checkouts (#3371). Required: a dropped registration fails at resolve, not by quietly mapping plain folders (#3408).</param>
internal sealed class ContextScopes(IApprovedProfile approvals, ICallerContextAccessor callers, IRepoCheckouts checkouts)
{
    /// <summary>The scope of the call running now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or SND-PROFILE-001 when the profile file is missing, or SND-PROFILE-004 when it can't be read.</returns>
    internal async ValueTask<Result<ContextScope>> Current(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileMissing.With("no profile yet, so no repo is mapped (fix: sindri repo add <path>)");
        }

        var scope = For(loaded.Profile, Caller(callers.Current));
        return WithoutPlainFolders(scope, checkouts);
    }

    // #3371: a profile repo whose path is a plain folder fails every git read (SND-CONTEXT-048). 'sindri repo add' refuses one, so only a hand-written
    // profile names one: every read leaves it out and names it, so one plain folder never fails the map of the others. A missing folder stays in, so
    // an unmounted or deleted repo keeps failing loudly and no map publishes without it.
    private static ContextScope WithoutPlainFolders(ContextScope scope, IRepoCheckouts checkouts)
    {
        var plain = scope.Repos.Where(r => checkouts.Of(r.Path) == RepoCheckout.PlainFolder).ToList();
        return plain.Count == 0 ? scope : scope with { Repos = [.. scope.Repos.Except(plain)], LeftOut = [.. plain.Select(static r => r.Name)] };
    }

    /// <summary>
    /// Whether a scope read found no profile yet (SND-PROFILE-001): a background service's tick is then quiet, never a failed run. A profile with no
    /// 'context:' section is a profile: the engine reads the missing section as the default settings, and so does every caller of this (#3126).
    /// </summary>
    /// <param name="scope">The read.</param>
    /// <returns>True for no profile.</returns>
    internal static bool NoProfile(Result<ContextScope> scope) =>
        scope.IsError && scope.Error.Code == SindriErrors.ProfileMissing.Code;

    /// <summary>The scope a profile and a caller make; SyncConfigured is whether the profile names the context-sync service.</summary>
    /// <param name="profile">The profile.</param>
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
    /// <param name="profile">The profile.</param>
    /// <returns>The repo entries, in profile order.</returns>
    internal static IReadOnlyList<ProfileRepoEntry> Entries(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path, RepoDenyGlobs.For(profile, r), [.. RepoConcepts.For(profile, r).Select(PinOf)], DemotePaths: RepoDemoteGlobs.For(profile, r)) { Roles = RepoRoleSets.For(profile, r) is { } sets ? [.. sets.Select(x => new RepoRole(x.Name, x.Demoted, x.Globs))] : null, Pages = Pages(profile, r) })];
    }

    /// <summary>The pin the engine reads an 'index.concepts' entry as.</summary>
    /// <param name="c">The entry.</param>
    /// <returns>The pin.</returns>
    internal static ConceptPin PinOf(ConceptSettings c) =>
        ConceptPin.Of(c.Id is { } id ? ConceptId.From(id.Value) : null, c.Name, c.Summary, c.Features, c.Paths).Declaring(c.DependsOn, c.Owns).WithAliases(c.Aliases);

    /// <summary>
    /// The pages of 'index.pages' as page pins: an id the profile leaves out is the name's slug (a clash takes '-2', '-3'), and the
    /// first 200 pages, 40 actions and 20 concepts are read and the rest ignored (the write refused them; a hand-edited profile gets
    /// a doctor row, never a refusal here).
    /// </summary>
    private static List<PagePin> Pages(SindriProfile profile, RepoEntry repo)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        return [.. RepoPages.For(profile, repo).Take(PageRules.MaxPages).Select(page => Page(page, taken))];
    }

    private static PagePin Page(PageSettings page, HashSet<string> taken)
    {
        var name = page.Name ?? string.Empty;
        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        return new PagePin(
            PageId.From(PageSlugs.Unique(page.Id is { } id && PageId.TryFrom(id.Value, out _) ? id.Value : PageSlugs.Of(name), taken)),
            name,
            page.Repo is { } written && ContextRepoName.TryFrom(written, out var repoName) ? repoName : null,
            page.Surface,
            [.. (page.Actions ?? []).Take(PageRules.MaxActions).Select(a => Action(a, actionIds))]);
    }

    private static PageActionPin Action(PageActionSettings action, HashSet<string> taken) => new(
        PageActionId.From(PageSlugs.Unique(action.Id is { } id && PageActionId.TryFrom(id.Value, out _) ? id.Value : PageSlugs.Of(action.Name), taken)),
        action.Name ?? string.Empty,
        action.Action,
        [.. (action.Concepts ?? []).Take(PageRules.MaxConcepts)]);

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
