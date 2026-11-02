namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a list row needs besides the instance view.</summary>
/// <param name="Now">The time the row is read at; the composer never reads a clock.</param>
/// <param name="NotStarted">True for a child that has not run yet (its chip reads QUEUED); an instance that is still starting reads so too.</param>
/// <param name="ItemTitle">The title of the item the instance is on, when the subject is that item; null shows the item's id.</param>
/// <param name="RecipeDisplayName">The recipe's display name; null serves none.</param>
/// <param name="LineStage">The current state of the root 'workflow.line' instance the row belongs to (its own state for the root row); null when its root is not a line instance.</param>
/// <param name="AgentIdle">True when the instance's current agent launch ended as idle with no report (read from the launches view by the caller).</param>
public sealed record InstanceSummaryContext(DateTimeOffset Now, bool NotStarted = false, string? ItemTitle = null, string? RecipeDisplayName = null, string? LineStage = null, bool AgentIdle = false);
