using Aw.Kernel;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Creates an owner's resource folders and files under the resources root: directories 0700, files 0600.</summary>
/// <param name="root">The resources root, <c>$AW_SINDRI_HOME/resources</c>.</param>
internal sealed class ResourceStore(string root)
{
    /// <summary>Gets the resources root.</summary>
    internal string Root { get; } = root;

    /// <summary>Creates what <paramref name="plan"/> lists. A second call changes nothing.</summary>
    /// <param name="owner">The owner; every path must sit under its folder.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>Success, or SND-RES-004 for a path outside the owner's folder.</returns>
    internal Result<Unit> Provision(ResourceOwner owner, ResourcePlan plan) => throw new NotSupportedException();
}
