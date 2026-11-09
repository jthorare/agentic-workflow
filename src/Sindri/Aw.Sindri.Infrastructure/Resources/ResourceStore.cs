using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Resources;

#pragma warning disable MA0182 // until the launch path provisions resources before a launch (agent-first PR 14 wires only the tests)

/// <summary>Creates an owner's resource folders and files under the resources root: directories 0700, files 0600.</summary>
/// <param name="root">The resources root, <c language="text">$AW_SINDRI_HOME/resources</c>.</param>
internal sealed class ResourceStore(string root)
{
    /// <summary>Gets the resources root.</summary>
    internal string Root { get; } = root;

    /// <summary>Creates what <paramref name="plan"/> lists. A second call changes nothing.</summary>
    /// <param name="owner">The owner; every path must sit under its folder.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>Success, or SND-RES-004 for a path outside the owner's folder.</returns>
    internal Result<Unit> Provision(ResourceOwner owner, ResourcePlan plan)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(plan);
        var home = Path.GetFullPath(Path.Combine(Root, owner.Folder));
        foreach (var item in plan.Create)
        {
            var full = Path.GetFullPath(item.Path);
            if (!string.Equals(Path.GetDirectoryName(full), home, StringComparison.Ordinal))
            {
                return SindriErrors.ResourcePathEscapes.With($"The {item.Kind.ToString().ToLowerInvariant()} '{item.Name}' would be created outside the folder of {owner}.");
            }
        }

        EnsureChain(Root, home);
        foreach (var item in plan.Create)
        {
            if (item.Kind == ResourceKind.Disk)
            {
                OwnerOnly.EnsureDirectory(item.Path);
            }
            else
            {
                SqliteFileProvisioner.Create(item.Path);
            }
        }

        return Unit.Value;
    }

    /// <summary>Makes <paramref name="root"/> and each folder down to <paramref name="leaf"/> 0700 when this call creates it.</summary>
    private static void EnsureChain(string root, string leaf)
    {
        var rootFull = Path.GetFullPath(root);
        OwnerOnly.EnsureDirectory(rootFull);
        var current = rootFull;
        foreach (var part in Path.GetRelativePath(rootFull, leaf).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            OwnerOnly.EnsureDirectory(current);
        }
    }
}
#pragma warning restore MA0182
