using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// Who may read what (spec §3), decided once for every transport: session reads are on unless the owner turned
/// 'context.sessions.enabled' off (SND-CONTEXT-020), and a session sees only the repo it names in X-Sindri-Repo. Any other
/// repo, or none, answers as an unknown anchor (SND-CONTEXT-003), never as a hint that it exists.
/// </summary>
public static class ScopeRules
{
    /// <summary>The repos <paramref name="caller"/> may read under <paramref name="settings"/>.</summary>
    /// <param name="settings">The checked settings (its 'repos' narrows the profile's).</param>
    /// <param name="repos">The profile's repos.</param>
    /// <param name="caller">The caller.</param>
    /// <returns>The repos in scope, in profile order; or SND-CONTEXT-001 (context.repos names a repo the profile doesn't have), 020 or 003.</returns>
    public static Result<IReadOnlyList<ContextRepo>> Resolve(ContextSettings settings, IReadOnlyList<ContextRepo> repos, ContextCaller caller)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(caller);
        var unknown = (settings.Repos ?? []).Where(n => !repos.Any(r => r.Name == n)).Select(n => n.Value).ToList();
        if (unknown.Count > 0)
        {
            return ContextErrors.SettingsInvalid.With(
                $"context.repos: '{unknown[0]}' is not a profile repo (repos: {string.Join(", ", repos.Select(r => r.Name.Value))})");
        }

        var mapped = Mapped(settings, repos).ToList();
        if (caller.Kind != ContextCallerKind.Session)
        {
            return mapped;
        }

        if (!settings.SessionsEnabled)
        {
            return ContextErrors.SessionsDisabled.With(
                "context reads from agent sessions are off (context.sessions.enabled: false in the approved profile); ask the human to turn them on in the profile and approve it");
        }

        if (caller.Repo is null)
        {
            return ContextErrors.McpRepoMissing.With(
                "this call doesn't say which repo it is for: send the header X-Sindri-Repo: <repo> (e.g. X-Sindri-Repo: agentic-workflow); the sindri CLI sends it when run from a repo's checkout, and 'sindri mcp register' sets it for MCP");
        }

        var own = mapped.Where(r => r.Name == caller.Repo).ToList();
        return own.Count == 1
            ? own
            : ContextErrors.UnknownAnchor.With("no context is mapped for this session's repo (X-Sindri-Repo)");
    }

    /// <summary>The repos the map is built from and maps: <c xml:lang="en">context.repos</c> narrows the profile's, whoever asks.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="repos">The profile's repos.</param>
    /// <returns>The mapped repos, in profile order.</returns>
    public static IReadOnlyList<ContextRepo> Mapped(ContextSettings settings, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        return settings.Repos is { } names ? [.. repos.Where(r => names.Contains(r.Name))] : [.. repos];
    }

    /// <summary>
    /// The profile's repos the engine can address. A name that isn't a <see cref="ContextRepoName"/> is left out; with a
    /// 'context:' section, <see cref="SettingsReader.Check(System.Text.Json.JsonElement?, IReadOnlyList{ProfileRepoEntry})"/>
    /// refuses such a name in scope at validate and approve.
    /// </summary>
    /// <param name="repos">The profile's repos as written.</param>
    /// <returns>The repos with usable names, in profile order, each with the built-in secret globs and the profile's deny paths.</returns>
    public static IReadOnlyList<ContextRepo> Usable(IReadOnlyList<ProfileRepoEntry> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);
        return [.. repos.Where(r => ContextRepoName.TryFrom(r.Name, out _)).Select(r => new ContextRepo(ContextRepoName.From(r.Name), r.Path) { DenyPaths = [.. PathDenylist.Defaults, .. r.DenyPaths ?? []], Concepts = r.Concepts ?? [], DemotePaths = r.DemotePaths ?? [], Roles = r.Roles ?? [] })];
    }

    /// <summary>
    /// The settings' repos against the profile's (only when the profile has a 'context:' section; a profile without one
    /// is never refused here): 'context.repos' names only profile repos, and every repo in scope has a usable name.
    /// </summary>
    /// <param name="hasSection">True when the profile has a 'context:' section.</param>
    /// <param name="settings">The checked settings.</param>
    /// <param name="repos">The profile's repos as written.</param>
    /// <returns>The settings, or SND-CONTEXT-001 naming the repo and the fix.</returns>
    internal static Result<ContextSettings> CheckRepos(bool hasSection, ContextSettings settings, IReadOnlyList<ProfileRepoEntry> repos)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        if (!hasSection)
        {
            return settings;
        }

        var inScope = settings.Repos is { } names ? repos.Where(r => names.Any(n => string.Equals(n.Value, r.Name, StringComparison.Ordinal))).ToList() : [.. repos];
        if (inScope.FirstOrDefault(r => !ContextRepoName.TryFrom(r.Name, out _)) is { } unusable)
        {
            return ContextErrors.SettingsInvalid.With(
                $"context.repos: the profile repo '{Printable(unusable.Name)}' has a name the context engine can't use (letters, digits, '.', '_' or '-'); rename it in repos:, or name the other repos in context.repos");
        }

        return Resolve(settings, Usable(repos), new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli)).Map(_ => settings);
    }

    private static string Printable(string text) => text.Length <= 100 && text.All(c => !char.IsControl(c)) ? text : "(a name with unusual characters)";
}
