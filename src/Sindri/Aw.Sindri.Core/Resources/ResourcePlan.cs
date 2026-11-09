using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Core.Resources;

/// <summary>What an owner's declarations come to.</summary>
/// <param name="Create">The databases and disks to create (all of them; creating twice changes nothing).</param>
/// <param name="Env">The environment variables for the run, by <see cref="ResourceEnv"/> name.</param>
/// <param name="Mounts">The state mounts to add to the sandbox plan: the name and host path of each database and disk.</param>
/// <param name="Limits">The sandbox limit overrides as declared (the limits resolver clamps them), or null when no sandbox is declared.</param>
public sealed record ResourcePlan(
    IReadOnlyList<PlannedResource> Create,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyList<ResourceMount> Mounts,
    ResourceLimitOverride? Limits);
