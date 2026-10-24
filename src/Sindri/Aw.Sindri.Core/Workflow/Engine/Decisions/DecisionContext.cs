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
/// <param name="Runtimes">The Steps this build runs off the desk, each with its runtime (block or agent; one map); a Step without one parks with SND-WF-010 unless it is deskCapable (W5).</param>
/// <param name="Children">The pinned child recipes, for a child wait's creation.</param>
public sealed record DecisionContext(
    ApprovalCheck Approval,
    BlockCatalog Catalog,
    IReadOnlyDictionary<BlockId, StepRuntime> Runtimes,
    IChildRecipeResolver Children)
{
    /// <summary>
    /// The profile's 'workflow.deskTimeout' (W20): a desk Step's timer runs at least this long, and the park its expiry
    /// leaves names the time the Step had. Defaults to W20's 2d (Rules.DeskTimeouts.DefaultFloor, which this namespace
    /// can't name without a namespace cycle).
    /// </summary>
    public TimeSpan DeskTimeout { get; init; } = TimeSpan.FromDays(2);

    /// <summary>
    /// Whether a Step's accepted report that runs a budget out still parks the instance on the report (the behavior before the 'token-budget-keeps-report-v1'
    /// patch, which a history recorded without the marker replays). Off: the report's transition applies, the spend is recorded, and the next Step start parks on the budget.
    /// </summary>
    public bool LegacyBudgetPark { get; init; }

    /// <summary>
    /// Whether an instance that starts counts a state's budget over its lifetime (the pooled rule before the 'token-budget-per-round-v1' patch, which a history
    /// recorded without the marker replays). Off: it starts under the per-round rule, where each round of a state has its own allowance (BudgetRules).
    /// </summary>
    public bool PooledBudget { get; init; }

    /// <summary>
    /// Whether a Step whose launch failed still counts a loop round (the behavior before the 'launch-failure-keeps-round-v1' patch, which a history recorded without the
    /// marker replays). Off: the round stays, and the third failed launch in a row parks.
    /// </summary>
    public bool LegacyLaunchRound { get; init; }

    /// <summary>
    /// Whether a state's timeout with 'then: park' parks at its first expiry (the behavior before the 'timeout-retries-before-park-v1' patch, which a history recorded
    /// without the marker replays). Off: the first expiry retries the state once (the timer re-arms in the same entry) and the second parks with 'timeout'; a desk Step still parks at once.
    /// </summary>
    public bool LegacyTimeoutPark { get; init; }

    /// <summary>
    /// Where this build runs a Step that is not at the desk (P8 D5, one map): <see cref="StepRuntime.Block"/> for a registered block runtime whose descriptor says
    /// <see cref="BlockRuntime.Block"/>, <see cref="StepRuntime.Agent"/> for a Step an agent session runs. A Step in neither is at the desk when it is deskCapable.
    /// </summary>
    /// <param name="step">The Step.</param>
    /// <returns>The runtime, or null when the build has none for it.</returns>
    internal StepRuntime? RuntimeOf(BlockId step) => Runtimes.TryGetValue(step, out var runtime) && (runtime == StepRuntime.Agent || (runtime == StepRuntime.Block && Catalog.Find(step) is { Runtime: BlockRuntime.Block }))
        ? runtime
        : null;
}
