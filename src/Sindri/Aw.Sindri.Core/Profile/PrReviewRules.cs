using System.Globalization;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The checks of the profile's top-level 'review:' block (PR review page spec §7, §9): SND-REVIEW-001 for a bad key, 003 for a 'command:' need and 005 for a repo
/// Sindri doesn't sync. SND-REVIEW-002 needs the Projects page, so <see cref="UnknownProject"/> runs where the page can be read.
/// </summary>
public static class PrReviewRules
{
    private const int MaxLogin = 39;
    /// <summary>Checks the block against the shape rules; the profile is needed for the repos 'context.sources.github.repos' syncs.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The first problem, or null.</returns>
    internal static Error? Check(SindriProfile profile)
    {
        if (profile.Review is not { } review)
        {
            return null;
        }

        return Login(review.Login) ?? Credential(review.Credential) ?? Settings(review) ?? Projects(review, SyncedRepos(profile.Context));
    }

    /// <summary>SND-REVIEW-002: the first 'review.projects[].project' that is not a card key on the Projects page.</summary>
    /// <param name="review">The review block, or null.</param>
    /// <param name="known">The keys of the cards on the Projects page (open, finished and hidden).</param>
    /// <returns>The error, or null.</returns>
    public static Error? UnknownProject(PrReviewSettings? review, IReadOnlySet<string> known)
    {
        ArgumentNullException.ThrowIfNull(known);
        var projects = review?.Projects ?? [];
        for (var i = 0; i < projects.Count; i++)
        {
            if (!known.Contains(projects[i].Project))
            {
                return SindriErrors.ReviewProjectUnknown.With(string.Create(CultureInfo.InvariantCulture, $"review.projects[{i}].project: No project {projects[i].Project}. `sindri projects` lists them."));
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="login"/> follows GitHub's name rules: 1 to 39 letters, digits or single hyphens, none leading or trailing.</summary>
    /// <param name="login">The text.</param>
    /// <returns>True when it is a login.</returns>
    public static bool IsLogin(string? login) =>
        login is { Length: >= 1 and <= MaxLogin }
        && login.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-')
        && login[0] != '-' && login[^1] != '-' && !login.Contains("--", StringComparison.Ordinal);

    /// <summary>The environment variable the review credential reads, or null when it is a 'command:' (nothing in any environment).</summary>
    /// <param name="review">The review block, or null.</param>
    /// <returns>The variable: the 'env:' name, else <see cref="DefaultCredentialVariable"/> when unset, else null.</returns>
    public static string? CredentialVariable(PrReviewSettings? review) => review?.Credential switch
    {
        null => DefaultCredentialVariable,
        { } c when c.StartsWith("env:", StringComparison.Ordinal) => c[4..],
        _ => null,
    };

    /// <summary>The variable the review credential reads when 'review.credential' is not set.</summary>
    public const string DefaultCredentialVariable = "AW_REVIEW_GITHUB_TOKEN";

    private static Error? Login(string? login) =>
        login is not null && !IsLogin(login)
            ? Invalid("review.login", $"'{login}' isn't a GitHub login (fix: use 1 to 39 letters, digits or single hyphens, not starting or ending with a hyphen)")
            : null;

    private static Error? Credential(string? credential) =>
        credential is not null && !ForgeRules.IsCredential(credential)
            ? Invalid("review.credential", "must be env:NAME (capitals, digits and '_') or command:<program and arguments>, never the token itself")
            : null;

    private static Error? Settings(PrReviewSettings review)
    {
        foreach (var (name, entry) in review.Settings ?? new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal))
        {
            var path = $"review.settings.{name}";
            if (name.Length == 0 || name == PrReviewDefaults.DefaultName)
            {
                return Invalid("review.settings", $"'{name}' is not a settings name: use a name other than 'default', which is built in");
            }

            var problem = Extends(review, name, entry, path) ?? Entry(entry, path);
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error? Extends(PrReviewSettings review, string name, PrReviewSettingsEntry entry, string path)
    {
        if (entry.Extends is not { } parent)
        {
            return null;
        }

        if (parent != PrReviewDefaults.DefaultName && review.Settings?.ContainsKey(parent) != true)
        {
            return Invalid($"{path}.extends", $"'{parent}' is not 'default' or a settings name here ({Names(review)})");
        }

        return PrReviewDefaults.Chain(review, name) is null
            ? Invalid($"{path}.extends", $"'{parent}' makes the chain loop back to '{name}'")
            : null;
    }

    private static string Names(PrReviewSettings review) =>
        string.Join(", ", (review.Settings ?? new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal)).Keys.Order(StringComparer.Ordinal).Prepend(PrReviewDefaults.DefaultName));

    private static Error? Entry(PrReviewSettingsEntry entry, string path) =>
        Panels(entry.Panels, $"{path}.panels") ?? Groups(entry.Groups, $"{path}.groups") ?? PrReviewCheckRules.Globs(entry.Collapse, $"{path}.collapse") ?? Risky(entry.Risky, $"{path}.risky") ?? PrReviewCheckRules.Checks(entry.Checks, $"{path}.checks");

    private static Error? Panels(IReadOnlyList<string>? panels, string path)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (var i = 0; i < (panels?.Count ?? 0); i++)
        {
            var panel = panels![i];   // why: the loop bound is 0 when panels is null
            if (!PrReviewDefaults.Panels.Contains(panel, StringComparer.Ordinal))
            {
                return Invalid(Index(path, i), $"'{panel}' is not a panel: {string.Join(", ", PrReviewDefaults.Panels)}");
            }

            if (!seen.Add(panel))
            {
                return Invalid(Index(path, i), $"'{panel}' is listed twice: name each panel at most once");
            }
        }

        return null;
    }

    private static Error? Groups(IReadOnlyList<PrReviewGroup>? groups, string path)
    {
        for (var i = 0; i < (groups?.Count ?? 0); i++)
        {
            var group = groups![i];   // why: the loop bound is 0 when groups is null
            if (string.IsNullOrWhiteSpace(group.Name))
            {
                return Invalid($"{Index(path, i)}.name", "must be non-empty text");
            }

            if (PrReviewCheckRules.Globs(group.Globs, $"{Index(path, i)}.globs") is { } bad)
            {
                return bad;
            }
        }

        return null;
    }

    private static Error? Risky(IReadOnlyList<PrReviewRisky>? risky, string path)
    {
        for (var i = 0; i < (risky?.Count ?? 0); i++)
        {
            var item = risky![i];   // why: the loop bound is 0 when risky is null
            if (PrReviewCheckRules.Globs(item.Globs, $"{Index(path, i)}.globs") is { } bad)
            {
                return bad;
            }

            if (string.IsNullOrWhiteSpace(item.Why))
            {
                return Invalid($"{Index(path, i)}.why", "must say why the path is risky");
            }
        }

        return null;
    }

    private static Error? Projects(PrReviewSettings review, HashSet<string> synced)
    {
        var projects = review.Projects ?? [];
        for (var i = 0; i < projects.Count; i++)
        {
            if (OneProject(review, projects[i], Index("review.projects", i), synced) is { } bad)
            {
                return bad;
            }
        }

        return null;
    }

    private static Error? OneProject(PrReviewSettings review, PrReviewProject project, string here, HashSet<string> synced)
    {
        if (string.IsNullOrWhiteSpace(project.Project))
        {
            return Invalid($"{here}.project", "must be a project id (prj_…) or a linear:project/<id> key");
        }

        if (project.Settings is { } name && name != PrReviewDefaults.DefaultName && review.Settings?.ContainsKey(name) != true)
        {
            return Invalid($"{here}.settings", $"'{name}' is not 'default' or a settings name here ({Names(review)})");
        }

        var repos = project.Repos ?? [];
        for (var r = 0; r < repos.Count; r++)
        {
            if (!synced.Contains(repos[r]))
            {
                return SindriErrors.ReviewRepoUnsynced.With($"{Index($"{here}.repos", r)}: Sindri doesn't sync {repos[r]}. Add it to context.sources.github.repos.");
            }
        }

        return null;
    }

    // The 'context.sources.github.repos' list: owner/name strings, read straight from the context JSON (the engine owns that section's checks).
    private static HashSet<string> SyncedRepos(JsonElement? context)
    {
        HashSet<string> repos = new(StringComparer.OrdinalIgnoreCase);
        if (context is { ValueKind: JsonValueKind.Object } c && c.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Object
            && sources.TryGetProperty("github", out var github) && github.ValueKind == JsonValueKind.Object
            && github.TryGetProperty("repos", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray().Where(static i => i.ValueKind == JsonValueKind.String))
            {
                repos.Add(item.GetString()!);   // why: the filter keeps strings, whose GetString is never null
            }
        }

        return repos;
    }

    internal static string Index(string path, int i) => string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]");

    internal static Error Invalid(string keyPath, string problem) => SindriErrors.ReviewInvalid.With($"{keyPath}: {problem}");
}
