using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// ISessionSandboxes over a launch's stored plan, '&lt;launch dir&gt;/areas/&lt;id&gt;/sandbox-plan.json', and its cgroup scope (sandbox spec §8): the plan gives the folder, the attended flag and the memory
/// ceiling, the scope's memory.current the memory in use. A plan that is gone or does not read is "details unavailable", never an error; a scope that is gone has no memory in use.
/// </summary>
/// <param name="launchDirectory">The launcher's directory.</param>
/// <param name="options">The package's AwJson options.</param>
/// <param name="stats">The cgroup reader.</param>
/// <param name="sliceCgroupPath">The slice's cgroup path under the cgroup root, with no leading slash.</param>
internal sealed class DiskSessionSandboxes(string launchDirectory, JsonSerializerOptions options, ICgroupStats stats, string sliceCgroupPath) : ISessionSandboxes
{
    public async ValueTask<SessionSandbox> Read(LaunchId launch, SandboxSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (await Plan(launch, cancellationToken).ConfigureAwait(false) is not { } plan)
        {
            return new SessionSandbox(SessionSandboxState.Unavailable, summary.Attended, Folder: null, Memory: null);
        }

        var used = stats.Read($"{sliceCgroupPath}/{SystemdScopeArgv.UnitFor(launch)}")?.MemoryCurrent;
        return new SessionSandbox(SessionSandboxState.On, plan.Attended, plan.Worktree ?? (plan.Writable is [var first, ..] ? first : null), new SessionMemory(used, plan.Limits.MemoryMaxBytes));
    }

    private async Task<SandboxPlan?> Plan(LaunchId launch, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(LaunchPaths.PlanFile(launchDirectory, launch), cancellationToken).ConfigureAwait(false);
            var parsed = AwJson.Deserialize<SandboxPlan>(bytes, options);
            return parsed.IsSuccess ? parsed.Value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;   // boundary: a plan that is gone or unreadable is "details unavailable"
        }
    }
}
