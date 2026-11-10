using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Resources;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// What the pure builder needs for a plain local process that talks over a pipe, such as a toolkit (agent-first PR 16). It has no item, no agent, no repo,
/// no Step policy, no model login and no hook canaries: only the owner, the command, the resources it may write and the hosts its connections reach.
/// </summary>
/// <param name="Launch">The supervisor's id for this process run (names its plan and scratch).</param>
/// <param name="Owner">Who the process belongs to, such as 'toolkit:github-triage'; shown as the mount reasons.</param>
/// <param name="Command">The absolute path of the binary; its folder is bound read-only.</param>
/// <param name="RunArea">The process's own folder on the host: its private /tmp lives in it.</param>
/// <param name="ResourceMounts">The databases and disks the process declared; each is bound read-write at its own path.</param>
/// <param name="Environment">The variables the process gets besides the allowlisted ones: the resource variables (<see cref="Core.Resources.ResourceEnv"/>).</param>
/// <param name="HostGroups">The hosts its connections reach: the only network the process has.</param>
/// <param name="Net">The network facts (the socket folder and the forwarders' ports).</param>
/// <param name="Host">The host facts.</param>
/// <param name="Limits">The process's own limits, clamped to the profile's, or null for the defaults.</param>
public sealed record ProcessSandboxRequest(
    LaunchId Launch,
    string Owner,
    string Command,
    string RunArea,
    IReadOnlyList<ResourceMount> ResourceMounts,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<PlanGroup> HostGroups,
    SandboxNetFacts Net,
    SandboxHostFacts Host,
    ResourceLimitOverride? Limits = null);
