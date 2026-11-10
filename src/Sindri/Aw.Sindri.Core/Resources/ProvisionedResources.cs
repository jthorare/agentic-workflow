using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Core.Resources;

/// <summary>What a run of one owner gets once its resources exist.</summary>
/// <param name="Env">The environment variables by <see cref="ResourceEnv"/> name; the value is a path, or a connection's name.</param>
/// <param name="Mounts">The state mounts for the sandbox plan.</param>
/// <param name="Limits">The sandbox limit overrides as declared, or null.</param>
public sealed record ProvisionedResources(
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyList<ResourceMount> Mounts,
    ResourceLimitOverride? Limits)
{
    /// <summary>Gets what an owner with no resources gets.</summary>
    public static ProvisionedResources None { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal), [], Limits: null);
}
