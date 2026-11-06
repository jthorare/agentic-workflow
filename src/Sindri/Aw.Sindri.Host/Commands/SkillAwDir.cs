namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The folder a skill writes its plans and reviews under for the repo at the working directory: '$AW_STATE_DIR/&lt;repo slug&gt;/' (AW_STATE_DIR, else '~/.agentic-workflow'), the slug being
/// the one skills/_shared/repo-slug.sh makes from the 'origin' URL ('owner-name'), else the checkout's folder name. Only to tell the owner where a trial writes; the launch computes its own.
/// </summary>
internal static class SkillAwDir
{
    /// <summary>The folder.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="currentDirectory">The working directory.</param>
    /// <returns>The path.</returns>
    internal static string Of(Func<string, string?> environment, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var state = environment("AW_STATE_DIR") is { Length: > 0 } set ? set : Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow");
        return Path.Combine(state, Slug(currentDirectory));
    }

    private static string Slug(string currentDirectory)
    {
        for (var directory = new DirectoryInfo(currentDirectory); directory is not null; directory = directory.Parent)
        {
            var git = Path.Combine(directory.FullName, ".git");
            var config = Directory.Exists(git) ? Path.Combine(git, "config") : null;
            if (config is null && !File.Exists(git))
            {
                continue;
            }

            return (config is not null && File.Exists(config) ? OriginOf(File.ReadLines(config)) : null) is { } slug ? slug : directory.Name;
        }

        return new DirectoryInfo(currentDirectory).Name;
    }

    /// <summary>The 'owner-name' of the origin URL in a git config, the way repo-slug.sh cuts it; null when there is no origin.</summary>
    private static string? OriginOf(IEnumerable<string> config)
    {
        var inOrigin = false;
        foreach (var raw in config)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inOrigin = string.Equals(line, "[remote \"origin\"]", StringComparison.Ordinal);
            }
            else if (inOrigin && line.StartsWith("url", StringComparison.Ordinal) && line.Split('=', 2) is [_, var url])
            {
                var trimmed = url.Trim().TrimEnd('/');
                trimmed = trimmed.EndsWith(".git", StringComparison.Ordinal) ? trimmed[..^4] : trimmed;
                var parts = trimmed.Split(['/', ':'], StringSplitOptions.RemoveEmptyEntries);
                return parts.Length >= 2 ? parts[^2] + "-" + parts[^1] : null;
            }
        }

        return null;
    }
}
