using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks of a repo's <c xml:lang="en">index.ignore</c> and <c xml:lang="en">index.demote</c>: per repo only, each name one of the repo's roles, no role in both lists.</summary>
internal static class RoleLists
{
    internal static Error? Check(SindriProfile profile)
    {
        if (profile.Index?.Ignore is { Count: > 0 })
        {
            return Invalid("index.ignore", "ignore lists are per repo: move it to repos.<name>.index.ignore");
        }

        if (profile.Index?.Demote is { Count: > 0 })
        {
            return Invalid("index.demote", "demote lists are per repo: move it to repos.<name>.index.demote");
        }

        return (profile.Repos ?? []).Select(r => Repo($"repos.{r.Name}.index", r.Index)).FirstOrDefault(static e => e is not null);
    }

    private static Error? Repo(string path, IndexSettings? index)
    {
        if (index is null)
        {
            return null;
        }

        var roles = index.Roles ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        return Names($"{path}.ignore", index.Ignore, roles, other: null) ?? Names($"{path}.demote", index.Demote, roles, index.Ignore);
    }

    private static Error? Names(string path, IReadOnlyList<string>? names, IReadOnlyDictionary<string, IReadOnlyList<string>> roles, IReadOnlyList<string>? other)
    {
        for (var i = 0; i < (names?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when names is null
            var name = names![i];
            var at = string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]");
            if (string.IsNullOrWhiteSpace(name))
            {
                return Invalid(at, "must be a role name, not empty");
            }

            if (!roles.ContainsKey(name))
            {
                return Invalid(at, $"'{name}' is not a role of this repo (index.roles)");
            }

            if (other?.Contains(name, StringComparer.Ordinal) == true)
            {
                return Invalid(at, $"'{name}' is in both index.ignore and index.demote: choose one");
            }
        }

        return null;
    }

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
