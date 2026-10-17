using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// What a decision reads besides the instance, the recipe and the trigger (spec §5.1). Everything a decision would
/// otherwise get from I/O is here, so every decision is a pure function the interpreter can call from workflow code;
/// the interpreter fills it from activities. Time is the interpreter's: its timers fire triggers (§5.5), and no
/// decision reads a clock.
/// </summary>
/// <param name="Approval">The approval check's answer for the instance's recipe.</param>
/// <param name="Catalog">The build's block and event descriptors (rungs, outcomes, outputs, deskCapable).</param>
/// <param name="Runtimes">The blocks and resources this build has a runtime for; a Step without one parks with SND-WF-010 unless it is deskCapable (W5).</param>
/// <param name="Children">The pinned child recipes, for a child wait's creation.</param>
public sealed record DecisionContext(
    ApprovalCheck Approval,
    BlockCatalog Catalog,
    IReadOnlySet<BlockId> Runtimes,
    IChildRecipeResolver Children)
{
    /// <summary>
    /// The profile's 'workflow.deskTimeout' (W20): a desk Step's timer runs at least this long, and the park its expiry
    /// leaves names the time the Step had. Defaults to W20's 2d (Runtime.DeskTimeouts.DefaultFloor, which this namespace
    /// can't name without a namespace cycle).
    /// </summary>
    public TimeSpan DeskTimeout { get; init; } = TimeSpan.FromDays(2);

    /// <summary>
    /// The Steps this build runs on the block runtime (DT spec §8.2): a Step in it whose descriptor says <see cref="BlockRuntime.Block"/>
    /// opens its run on <see cref="StepRuntime.Block"/>. Empty by default, which is the desk path every history before the block runtime took.
    /// </summary>
    public IReadOnlySet<BlockId> BlockRuntimes { get; init; } = new HashSet<BlockId>();

    /// <summary>Whether <paramref name="step"/> opens a block run: this build registered its runtime and its descriptor says <see cref="BlockRuntime.Block"/>. The one rule entry and Runnable both ask.</summary>
    /// <param name="step">The Step.</param>
    /// <returns>True when its run is the engine's.</returns>
    internal bool RunsAsBlock(BlockId step) => BlockRuntimes.Contains(step) && Catalog.Find(step) is { Runtime: BlockRuntime.Block };
}
