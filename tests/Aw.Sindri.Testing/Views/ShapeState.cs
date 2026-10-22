#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Testing.Views;

/// <summary>One state of a fixture recipe: where it sits in the path order (spec 3.10) and what the recipe declares for it.</summary>
/// <param name="Id">The state id.</param>
/// <param name="Order">1-based path order, null for a branch state.</param>
/// <param name="BranchOf">The state a branch folds under.</param>
/// <param name="Terminal">A final state.</param>
/// <param name="Desk">What the fixtures serve for the state: a desk task. The engine serves every active state as one until the agent runtime (P8); see <see cref="RunsAtDesk"/>.</param>
/// <param name="Child">It waits on a child recipe.</param>
/// <param name="Wait">It is a wait state (a child wait is one too).</param>
/// <param name="Timeout">The declared timeout; every active and wait state has one.</param>
/// <param name="BudgetTokens">The declared state budget, in tokens.</param>
/// <param name="OnParked">A child wait's reaction to a parked child.</param>
/// <param name="Block">The Step block an active state uses, where the recipe is a file under recipes/.</param>
/// <param name="ChildRecipe">The recipe a child wait starts.</param>
/// <param name="AgentBlock">The agent-runtime Block an active state of a fixture-only recipe names (the P8-era product the screens fixture shows); null for the recipes that exist under recipes/, which run at the desk until P8.</param>
public sealed record ShapeState(string Id, int? Order, string? BranchOf = null, bool Terminal = false, bool Desk = false, bool Child = false, bool Wait = false,
    ShapeTimeout? Timeout = null, int? BudgetTokens = null, OnParked? OnParked = null, string? Block = null, string? ChildRecipe = null, string? AgentBlock = null)
{
    /// <summary>The state kind the declaration has.</summary>
    public StateKind Kind => Terminal ? StateKind.Terminal : Child || Wait ? StateKind.Wait : StateKind.Active;

    /// <summary>
    /// The engine runs the state on the desk runtime: an active state of a recipe that exists under recipes/ (its Block is deskCapable and has
    /// no agent runtime until P8; WF 12 table, W5, W20). A fixture-only recipe names an agent-runtime Block instead and runs the state as an agent.
    /// </summary>
    public bool RunsAtDesk => Kind == StateKind.Active && AgentBlock is null;

    /// <summary>The timeout the engine serves: at the desk at least the desk floor, and always ending in park (W20).</summary>
    public ShapeTimeout? Served => Timeout is null ? null : RunsAtDesk ? new ShapeTimeout(DeskTimeouts.Wait(new TimeoutSpec(RecipeDuration.From(M(Timeout.After)), Timeout.Then), DeskTimeouts.DefaultFloor), TimeoutAction.Park) : Timeout;

    private static string M(TimeSpan span) => ((long)span.TotalMinutes).ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
}
