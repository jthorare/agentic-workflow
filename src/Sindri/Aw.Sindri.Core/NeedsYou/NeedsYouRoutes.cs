using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// The deep links a needs-you 'open' names (q65), in the app's DeepLink forms: the one place each route is spelled, so a row, the instance detail and
/// a desk notice that point at the same place say it the same way.
/// </summary>
public static class NeedsYouRoutes
{
    /// <summary>The setup screen.</summary>
    public const string Setup = "setup";

    /// <summary>The instance at <paramref name="address"/>: 'workflow/&lt;address&gt;'.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>The route.</returns>
    public static string Workflow(InstanceAddress address) => "workflow/" + address.Value;

    /// <summary>The desk of the instance at <paramref name="address"/>; the app draws a desk in the instance's frame, so it is that instance's route.</summary>
    /// <param name="address">The instance whose desk is open.</param>
    /// <returns>The route.</returns>
    public static string Desk(InstanceAddress address) => Workflow(address);

    /// <summary>A park of the root at <paramref name="root"/>: the deepest parked descendant (or the mirrored child that decides) when there is one, else the root.</summary>
    /// <param name="root">The root instance's address.</param>
    /// <param name="deepest">The descendant that decides the answer; null when the root decides it.</param>
    /// <returns>The route.</returns>
    public static string Park(InstanceAddress root, InstanceAddress? deepest) => Workflow(deepest ?? root);

    /// <summary>A decision: 'decision/&lt;DEC-…&gt;'.</summary>
    /// <param name="id">The decision's id.</param>
    /// <returns>The route.</returns>
    public static string Decision(DecisionId id) => "decision/" + id.Value;

    /// <summary>A service: 'service/&lt;name&gt;'.</summary>
    /// <param name="name">The service's name.</param>
    /// <returns>The route.</returns>
    public static string Service(string name) => "service/" + name;

    /// <summary>A recipe: 'recipe/&lt;id&gt;' (no '@hash': a row carries none).</summary>
    /// <param name="id">The recipe's id.</param>
    /// <returns>The route.</returns>
    public static string Recipe(RecipeId id) => "recipe/" + id.Value;

    /// <summary>The row's own sheet, for a row with no other place: 'needs-you/&lt;rowId&gt;'.</summary>
    /// <param name="id">The row's id.</param>
    /// <returns>The route.</returns>
    public static string Row(NeedsYouRowId id) => "needs-you/" + id.Value;
}
