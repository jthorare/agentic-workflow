using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Skills;

/// <summary>The environment a skill session gets (skill flows spec §3.3, D8).</summary>
public static class SkillLaunchEnv
{
    /// <summary>The folder in the run directory the launcher makes for a Step's outputs; the per-launch result file lives in it.</summary>
    internal const string OutputFolder = ".sindri-out";

    /// <summary>The per-launch result file: '&lt;run dir&gt;/.sindri-out/result-&lt;launch&gt;.json'.</summary>
    /// <param name="runDirectory">The launch's run directory.</param>
    /// <param name="launch">The launch id.</param>
    /// <returns>The path.</returns>
    public static string ResultFileOf(string runDirectory, LaunchId launch)
    {
        ArgumentNullException.ThrowIfNull(runDirectory);
        return $"{runDirectory.TrimEnd('/')}/{OutputFolder}/result-{launch.Value}.json";
    }

    /// <summary>AW_RESULT=1, AW_RESULT_SKILL, AW_RESULT_FILE and AW_DIR=&lt;state dir&gt;/&lt;slug&gt;/.</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <param name="launch">The launch id.</param>
    /// <param name="runDirectory">The launch's run directory.</param>
    /// <param name="stateDirectory">The toolkit state directory (AW_STATE_DIR).</param>
    /// <param name="repoSlug">The repo's slug, computed once by the host.</param>
    /// <returns>The variables.</returns>
    public static IReadOnlyDictionary<string, string> For(string skill, LaunchId launch, string runDirectory, string stateDirectory, string repoSlug)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(stateDirectory);
        ArgumentNullException.ThrowIfNull(repoSlug);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AW_RESULT"] = "1",
            ["AW_RESULT_SKILL"] = skill,
            ["AW_RESULT_FILE"] = ResultFileOf(runDirectory, launch),
            ["AW_DIR"] = $"{stateDirectory.TrimEnd('/')}/{repoSlug}/",
        };
    }

    /// <summary>The three variables that need no repo slug (a launch with no sandbox plan carries these; AW_DIR then stays the skill's own default).</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <param name="launch">The launch id.</param>
    /// <param name="runDirectory">The launch's run directory.</param>
    /// <returns>The variables.</returns>
    public static IReadOnlyDictionary<string, string> ResultOnly(string skill, LaunchId launch, string runDirectory)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AW_RESULT"] = "1",
            ["AW_RESULT_SKILL"] = skill,
            ["AW_RESULT_FILE"] = ResultFileOf(runDirectory, launch),
        };
    }

    /// <summary>The repo slug the skills compute with skills/_shared/repo-slug.sh: the origin remote's 'owner/name' as 'owner-name', else the checkout's folder name.</summary>
    /// <param name="originUrl">The origin remote's URL, or null when there is none.</param>
    /// <param name="repoPath">The repo checkout.</param>
    /// <returns>The slug.</returns>
    public static string RepoSlugOf(string? originUrl, string repoPath)
    {
        ArgumentNullException.ThrowIfNull(repoPath);
        if (string.IsNullOrWhiteSpace(originUrl))
        {
            var trimmed = repoPath.TrimEnd('/');
            return trimmed[(trimmed.LastIndexOf('/') + 1)..];
        }

        var url = originUrl.Trim();
        if (url.EndsWith(".git", StringComparison.Ordinal))
        {
            url = url[..^4];
        }

        var parts = url.Split(['/', ':'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^2] + "-" + parts[^1] : url.Replace('/', '-');
    }
}
