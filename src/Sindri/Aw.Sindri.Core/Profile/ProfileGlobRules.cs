using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The glob checks of the profile's <c xml:lang="en">index</c> keys: <c xml:lang="en">denyPaths</c> everywhere, and the per-repo <c xml:lang="en">roles</c>.</summary>
internal static class ProfileGlobRules
{
    /// <summary>The longest glob 'index.denyPaths' takes.</summary>
    private const int MaxGlob = 300;

    private static readonly string[] _roleTags = ["prefix:", "suffix:", "regex:"];

    /// <summary>Checks every 'index.denyPaths' glob (the profile's and each repo's) and every repo's 'index.roles': relative to the repo, '/'-separated, no '..'.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The first problem, or null.</returns>
    internal static Error? Check(SindriProfile profile)
    {
        if (profile.Index?.Roles is { Count: > 0 })
        {
            return Invalid("index.roles", "roles are per repo: move them to repos.<name>.index.roles");
        }

        return FirstProblem(DenyLists(profile), GlobProblem)
            ?? FirstProblem((profile.Repos ?? []).SelectMany(r => Roles($"repos.{r.Name}.index", r.Index)), RoleProblem)
            ?? RoleLists.Check(profile);
    }

    internal static string? GlobProblem(string glob) => true switch
    {
        _ when string.IsNullOrWhiteSpace(glob) => "must be a non-empty glob",
        _ when glob.Length > MaxGlob => string.Create(CultureInfo.InvariantCulture, $"is over {MaxGlob} characters"),
        _ when glob.StartsWith('/') || glob.Contains(':', StringComparison.Ordinal) => "must be relative to the repo root (no leading '/' and no drive or scheme)",
        _ when glob.Contains('\\', StringComparison.Ordinal) => "must use '/' separators",
        _ when glob.Split('/').Contains("..", StringComparer.Ordinal) => "must not contain '..'",
        _ when glob.Contains('\0', StringComparison.Ordinal) => "must not contain a NUL character",
        _ => null,
    };

    private static Error? FirstProblem(IEnumerable<(string Path, IReadOnlyList<string> Globs)> lists, Func<string, string?> problemOf)
    {
        foreach (var (path, globs) in lists)
        {
            for (var i = 0; i < globs.Count; i++)
            {
                if (problemOf(globs[i]) is { } problem)
                {
                    return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), problem);
                }
            }
        }

        return null;
    }

    private static IEnumerable<(string Path, IReadOnlyList<string> Globs)> DenyLists(SindriProfile profile)
    {
        if (profile.Index?.DenyPaths is { } shared)
        {
            yield return ("index.denyPaths", shared);
        }

        foreach (var repo in (profile.Repos ?? []).Where(r => r.Index?.DenyPaths is not null))
        {
            yield return ($"repos.{repo.Name}.index.denyPaths", repo.Index!.DenyPaths!);   // why: the filter keeps repos with a list
        }
    }

    private static IEnumerable<(string Path, IReadOnlyList<string> Globs)> Roles(string path, IndexSettings? index) =>
        (index?.Roles ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)).Select(r => ($"{path}.roles.{r.Key}", r.Value));

    // A role entry is a glob or a 'prefix:', 'suffix:' or 'regex:' text matched against one path segment (the context engine's PathDenylist).
    private static string? RoleProblem(string entry)
    {
        var tag = _roleTags.FirstOrDefault(t => entry.StartsWith(t, StringComparison.Ordinal));
        if (tag is null)
        {
            return GlobProblem(entry);
        }

        var text = entry[tag.Length..];
        if (text.Length is 0 or > MaxGlob)
        {
            return $"'{tag}' takes a text of 1 to {MaxGlob} characters";
        }

        if (string.Equals(tag, "regex:", StringComparison.Ordinal))
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(text, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                return "is not a valid regular expression";
            }

            return null;
        }

        return text.Contains('/', StringComparison.Ordinal) ? $"'{tag}' matches one path segment: no '/'" : null;
    }

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
