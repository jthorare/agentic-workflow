using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Skills;

/// <summary>Builds the descriptor 'step.skill.&lt;id&gt;' from a block's manifest entry (skill flows spec §3.2) and names its fact keys (D1).</summary>
public static class SkillDescriptors
{
    /// <summary>The fact key of an output: 'fact:&lt;block id in camelCase&gt;&lt;Output, first letter capitalised&gt;'.</summary>
    /// <param name="blockName">The kebab-case block id.</param>
    /// <param name="output">The output's name.</param>
    /// <returns>The key; an error when it is longer than 64 characters.</returns>
    public static Result<string> FactKeyOf(string blockName, string output) => throw new NotSupportedException();

    /// <summary>Builds the descriptor: runtime agent, one Text parameter per input, outputs required on the success outcome, the mapped outcomes, a Version derived from the canonical hash of the entry, and an interface hash that includes the args and the outcome map.</summary>
    /// <param name="manifest">The manifest entry.</param>
    /// <returns>The descriptor.</returns>
    public static Result<BlockDescriptor> Build(SkillBlockManifest manifest) => throw new NotSupportedException();

    /// <summary>The canonical hash of a manifest entry, args, 'from' and 'values' included.</summary>
    /// <param name="manifest">The manifest entry.</param>
    /// <returns>The hash in lower-case hex.</returns>
    public static string HashOf(SkillBlockManifest manifest) => throw new NotSupportedException();
}
