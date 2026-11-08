using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Routing;

/// <summary>Decides which lanes should move to another provider.</summary>
public static class RoutePlanner
{
    /// <summary>Plans the switches.</summary>
    /// <param name="routing">The routing config.</param>
    /// <param name="lanes">Every lane's standing (the meta is not a lane here).</param>
    /// <returns>The suggestions and the skipped lanes.</returns>
    public static RoutePlan Plan(RoutingConfig routing, IReadOnlyList<LaneStanding> lanes) => throw new NotSupportedException();
}
