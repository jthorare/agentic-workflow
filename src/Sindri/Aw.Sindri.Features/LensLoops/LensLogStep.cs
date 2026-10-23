using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// 'step.lens.log', on the block runtime (line spec §4.1): at the round's end 'clean' (0 CRITICAL, 0 HIGH), else 'findings' with those findings as 'fact:findings', or 'needsOwner' (the same findings
/// and every question of the round, all at once, as the list 'fact:escalation') when any of them carries a typed escalation (<see cref="Escalation"/>).
/// </summary>
internal sealed class LensLogStep() : RoundLogStep(LensScale.Design), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.lens.log");

    /// <inheritdoc/>
    protected override BlockStepResult Ended(RoundLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Stopping() is not { Count: > 0 } stopping)
        {
            return Done("clean", ("lensRounds", log.Rounds));
        }

        var findings = new JsonArray([.. stopping]);
        return Escalation.All(stopping) is { Count: > 0 } escalation
            ? Done("needsOwner", ("lensRounds", log.Rounds), ("findings", findings), ("escalation", escalation))
            : Done("findings", ("lensRounds", log.Rounds), ("findings", findings));
    }
}
