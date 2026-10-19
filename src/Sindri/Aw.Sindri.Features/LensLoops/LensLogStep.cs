using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// 'step.lens.log', on the block runtime (line spec §4.1): at the round's end 'clean' (0 CRITICAL, 0 HIGH), else 'findings' with those findings as 'fact:findings'.
/// </summary>
internal sealed class LensLogStep() : RoundLogStep(LensScale.Design), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.lens.log");

    /// <inheritdoc/>
    protected override BlockStepResult Ended(RoundLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return log.Stopping() is { Count: > 0 } stopping
            ? Done("findings", ("lensRounds", log.Rounds), ("findings", new JsonArray([.. stopping])))
            : Done("clean", ("lensRounds", log.Rounds));
    }
}
