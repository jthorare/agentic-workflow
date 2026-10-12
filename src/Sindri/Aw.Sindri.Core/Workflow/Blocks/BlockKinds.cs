using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Maps a block id's prefix to its kind.</summary>
public static class BlockKinds
{
    /// <summary>The kind <paramref name="id"/>'s prefix names.</summary>
    /// <param name="id">A block id.</param>
    /// <returns>Its kind.</returns>
    public static BlockKind Of(BlockId id) => id.Prefix switch
    {
        "gate" => BlockKind.Gate,
        "evidence" => BlockKind.EvidenceProvider,
        "step" => BlockKind.Step,
        "effect" => BlockKind.Effect,
        _ => BlockKind.StepInput,
    };
}
