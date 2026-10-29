using System.Globalization;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The box facts of a Linux launch's sandbox step: paths from the launcher's own options, memory and cores from the machine port, the control plane's port, the catalog's policy form.</summary>
/// <param name="Launcher">The launcher's files.</param>
/// <param name="Catalog">The agent catalog.</param>
/// <param name="Processes">Runs git.</param>
/// <param name="HostFacts">Reads the host facts at each launch (the switch step's BoxHostFacts.Read).</param>
/// <param name="Control">The running control plane's endpoint record, or null when unknown.</param>
/// <param name="Counters">What a launch's denial counter is made from, or null to count no denials.</param>
internal sealed class LinuxSandboxLaunchInputs(LauncherOptions Launcher, IAgentCatalog Catalog, ISandboxProcesses Processes, Func<SandboxHostFacts> HostFacts, Func<ControlEndpoint?> Control, DenialCounterParts? Counters = null) : ISandboxLaunchInputs
{
    /// <summary>The proxy port inside every sandbox (its network namespace is private, so one fixed port can't clash).</summary>
    internal const int FixedProxyPort = 3128;

    private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public SandboxHostFacts Host() => HostFacts();

    /// <inheritdoc/>
    public ValueTask<string> GitCommonDir(string repoPath, CancellationToken cancellationToken)
    {
        var ran = Processes.Run("git", ["-C", repoPath, "rev-parse", "--git-common-dir"], _gitTimeout);
        var printed = ran.Succeeded ? ran.Output.Trim() : string.Empty;
        return ValueTask.FromResult(printed.Length == 0 ? Path.Combine(repoPath, ".git") : Path.GetFullPath(printed, repoPath));
    }

    /// <inheritdoc/>
    public string RunAreaFor(LaunchId launch) => LaunchPaths.RunArea(Launcher.LaunchDirectory, launch);

    /// <inheritdoc/>
    public string? RunDirectoryFor(LaunchRequest request) => request.Attended ? AttendedOpen.RunDirectory(Launcher.SindriHome, request.Launch) : StepEnvironment.RunDirectoryOf(request, Launcher.SindriHome);

    /// <inheritdoc/>
    public string NetDirFor(LaunchId launch)
    {
        var runtime = string.Create(CultureInfo.InvariantCulture, $"/run/user/{HostFacts().Uid}");
        return NetDir(Directory.Exists(runtime) ? runtime : "/tmp", launch);
    }

    /// <inheritdoc/>
    public int SindriPort() => Control()?.Url.Port ?? 0;

    /// <inheritdoc/>
    public int ProxyPort() => FixedProxyPort;

    /// <inheritdoc/>
    public string PolicyDirectory() => Launcher.PolicyDirectory;

    /// <inheritdoc/>
    public PolicyForm PolicyFormOf(AgentId agent) => Catalog.Resolve(agent)?.PolicyForm ?? PolicyForm.None;

    /// <inheritdoc/>
    public string? TranscriptGlob(AgentId agent) => Catalog.Resolve(agent)?.Transcripts;

    /// <inheritdoc/>
    public string? ReportedSession(LaunchId launch)
    {
        try
        {
            var path = Path.Combine(Launcher.LaunchesDirectory, LaunchFiles.SessionIdName(launch));
            return File.Exists(path) ? File.ReadLines(path).FirstOrDefault()?.Trim() is { Length: > 0 } id ? id : null : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public DenialCounter? DenialCounterFor(LaunchId launch) =>
        Counters is { } parts
            ? new DenialCounter(launch, LaunchPaths.DenialsFile(Launcher.LaunchDirectory, launch), parts.Json, parts.Scrubber, parts.Clock) { Sink = parts.Sink }
            : null;

    /// <summary>The socket directory of a launch: short, because a unix socket path holds about 100 characters (<c lang="text">&lt;base&gt;/aw-net/&lt;12 hex&gt;</c>).</summary>
    /// <param name="baseDirectory">/run/user/&lt;uid&gt; or /tmp.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    internal static string NetDir(string baseDirectory, LaunchId launch) =>
        Path.Combine(baseDirectory, "aw-net", launch.Value.Replace("LCH-", string.Empty, StringComparison.Ordinal)[..12]);
}
