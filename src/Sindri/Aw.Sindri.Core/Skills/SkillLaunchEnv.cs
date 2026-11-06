using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Skills;

/// <summary>The environment a skill session gets (skill flows spec §3.3, D8).</summary>
public static class SkillLaunchEnv
{
    /// <summary>The per-launch result file: '&lt;run dir&gt;/.sindri-out/result-&lt;launch&gt;.json'.</summary>
    /// <param name="runDirectory">The launch's run directory.</param>
    /// <param name="launch">The launch id.</param>
    /// <returns>The path.</returns>
    public static string ResultFileOf(string runDirectory, LaunchId launch) => throw new NotSupportedException();

    /// <summary>AW_RESULT=1, AW_RESULT_SKILL, AW_RESULT_FILE and AW_DIR=&lt;state dir&gt;/&lt;slug&gt;/.</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <param name="launch">The launch id.</param>
    /// <param name="runDirectory">The launch's run directory.</param>
    /// <param name="stateDirectory">The toolkit state directory (AW_STATE_DIR).</param>
    /// <param name="repoSlug">The repo's slug, computed once by the host.</param>
    /// <returns>The variables.</returns>
    public static IReadOnlyDictionary<string, string> For(string skill, LaunchId launch, string runDirectory, string stateDirectory, string repoSlug) => throw new NotSupportedException();
}
