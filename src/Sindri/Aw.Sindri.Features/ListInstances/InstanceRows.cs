using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>Builds the list rows at the time the list is read: the ages and timeouts come from that time, and each row's recipe carries its display name (setup-once spec §7.7).</summary>
/// <param name="clock">The time the rows are read at.</param>
/// <param name="names">The recipes' display names; null serves none.</param>
internal sealed class InstanceRows(TimeProvider clock, IRecipeDisplayNames? names = null)
{
    /// <summary>The time a list read now is as of.</summary>
    /// <returns>The instant.</returns>
    public DateTimeOffset Now() => clock.GetUtcNow();

    /// <summary>The row of <paramref name="view"/> as of <paramref name="now"/>.</summary>
    /// <param name="view">The instance view.</param>
    /// <param name="titles">The titles of the items the views are on.</param>
    /// <param name="now">The time the list is read at.</param>
    /// <param name="lineStage">The current state of the line instance the row belongs to; null when it belongs to none.</param>
    /// <param name="agentIdle">True when the instance's current agent launch ended as idle with no report.</param>
    /// <returns>The row.</returns>
    public InstanceSummary Row(InstanceView view, ItemTitles titles, DateTimeOffset now, string? lineStage = null, bool agentIdle = false) =>
        InstanceSummaries.Of(view, new InstanceSummaryContext(now, ItemTitle: titles.For(view), RecipeDisplayName: names?.Of(view.Recipe.Id), LineStage: lineStage, AgentIdle: agentIdle));
}
