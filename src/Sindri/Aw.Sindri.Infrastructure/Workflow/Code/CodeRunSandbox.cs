using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>The sandbox request of one Code run: the runner's folder read-only, the private /tmp as the only writable place, the repo read-only, no host and so no network.</summary>
internal static class CodeRunSandbox
{
    /// <summary>Builds the request.</summary>
    /// <param name="launch">The id naming this run's plan and scratch.</param>
    /// <param name="runnerPath">The runner binary's absolute path.</param>
    /// <param name="runArea">The run's own folder on the host.</param>
    /// <param name="repoPath">The repo's folder the code may read, or null for none.</param>
    /// <param name="host">The host facts.</param>
    /// <returns>The request for <see cref="ISandbox.PlanProcess"/>.</returns>
    internal static ProcessSandboxRequest Request(LaunchId launch, string runnerPath, string runArea, string? repoPath, SandboxHostFacts host) =>
        new(launch, "code", runnerPath, runArea, [], new Dictionary<string, string>(StringComparer.Ordinal), [], new SandboxNetFacts(runArea + "/net", SindriPort: 0, ProxyPort: 0), host, Limits: null, ReadOnlyPaths: repoPath is null ? null : [repoPath]);
}
