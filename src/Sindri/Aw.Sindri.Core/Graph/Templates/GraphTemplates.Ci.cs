namespace Aw.Sindri.Core.Graph.Templates;

public static partial class GraphTemplates
{
    /// <summary>Says whether a starter template can read a repo's CI (weekday-check and daily-digest), so a caller looks the source up only for those.</summary>
    /// <param name="name">The template's name.</param>
    /// <returns>True when <see cref="Find"/> builds a CI variant for the name.</returns>
    public static bool ReadsCi(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Equals(name, "weekday-check", StringComparison.Ordinal) || string.Equals(name, "daily-digest", StringComparison.Ordinal);
    }

    // red stage (#3534): the CI variants come next; a non-null source for weekday-check or daily-digest is not built yet
    private static GraphTemplate? CiTemplate(string name, GitHubCiSource ci)
        => ReadsCi(name)
            ? throw new NotSupportedException($"the CI variant of '{name}' for {ci.Owner}/{ci.Repo} is not built yet")
            : null;
}
