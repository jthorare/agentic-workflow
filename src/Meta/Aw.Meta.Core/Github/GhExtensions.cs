namespace Aw.Meta.Core.Github;

/// <summary>Reads what `gh extension list` prints.</summary>
public static class GhExtensions
{
    /// <summary>The command that installs the gh stack extension.</summary>
    public const string StackInstall = "gh extension install github/gh-stack";

    /// <summary>Whether the gh stack extension is installed: a row (name, repo and version, tab separated) named "gh stack", or from a repo called gh-stack.</summary>
    /// <param name="list">The output of `gh extension list`.</param>
    /// <returns>True when the list holds the extension.</returns>
    public static bool HasStack(string list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return list.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(row => row.Split('\t', StringSplitOptions.TrimEntries))
            .Any(columns => columns.Length > 0 && (string.Equals(columns[0], "gh stack", StringComparison.Ordinal)
                || (columns.Length > 1 && columns[1].EndsWith("/gh-stack", StringComparison.Ordinal))));
    }
}
