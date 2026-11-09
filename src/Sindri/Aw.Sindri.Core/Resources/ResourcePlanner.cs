using Aw.Kernel;

namespace Aw.Sindri.Core.Resources;

/// <summary>Turns an owner's declarations and the box's limits into what to create, the environment, the mounts and the limit overrides. Pure.</summary>
public static class ResourcePlanner
{
    /// <summary>Plans an owner's resources.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="spec">What it declares.</param>
    /// <param name="resourcesRoot">The resources root, <c>$AW_SINDRI_HOME/resources</c>.</param>
    /// <param name="box">What the box allows one session.</param>
    /// <returns>The plan, or SND-RES-002 for a name used twice, SND-RES-006 for a bad name or size.</returns>
    public static Result<ResourcePlan> Plan(ResourceOwner owner, ResourceSpec spec, string resourcesRoot, ResourceBoxLimits box) =>
        throw new NotSupportedException();
}
