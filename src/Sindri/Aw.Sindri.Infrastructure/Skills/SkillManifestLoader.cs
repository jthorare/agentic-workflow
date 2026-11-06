using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>Reads every 'skills/*/block.yaml' under one folder into skill blocks (skill flows spec §3.2). A manifest that fails the checks is skipped with a reason, never thrown; an empty or missing folder is an empty list.</summary>
internal static class SkillManifestLoader
{
    /// <summary>Loads the folder.</summary>
    /// <param name="skillsFolder">The skills folder: the one that holds '&lt;skill&gt;/block.yaml'.</param>
    /// <returns>The blocks that loaded and the ones skipped.</returns>
    internal static SkillBlocks Load(string skillsFolder) => throw new NotSupportedException();
}
