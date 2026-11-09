namespace Aw.Sindri.Core.Resources;

/// <summary>What an owner's declarations come to.</summary>
/// <param name="Create">The databases and disks to create (all of them; creating twice changes nothing).</param>
/// <param name="Env">The environment variables for the run, by <see cref="ResourceEnv"/> name.</param>
/// <param name="Mounts">The read-write mounts to add to the sandbox plan.</param>
/// <param name="Limits">The sandbox limit overrides.</param>
public sealed record ResourcePlan(
    IReadOnlyList<PlannedResource> Create,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyList<ResourceMount> Mounts,
    ResourceLimitOverrides Limits);
