using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The sandbox step of a Step launch (sandbox spec §12 SB-5a): the argv and environment it runs with, and the release of what it prepared.</summary>
internal sealed partial class Launcher
{
    /// <summary>What a launch runs with after the sandbox step: the argv, the environment and the summary (null when it ran unconfined).</summary>
    private sealed record Confined(IReadOnlyList<string> Argv, Dictionary<string, string> Env, SandboxSummary? Summary, string? RunArea = null);

    /// <summary>'sindri session wrap --launch id [--limits file] --': a confined launch hands the watchdog its scope's limits, or an OOM kill ends the run as 'exit' (#1553).</summary>
    private List<string> WrapCommand(LaunchId launch, string? runArea)
    {
        var wrapped = new List<string> { parts.Options.SindriExecutable, "session", "wrap", "--launch", launch.Value };
        if (runArea is not null)
        {
            wrapped.AddRange(["--limits", runArea.TrimEnd('/') + "/" + LaunchPaths.WrapLimitsName]);
        }

        wrapped.Add("--");
        return wrapped;
    }

    /// <summary>A launch with a sandbox (a Step's or a session the owner opens, with no unconfined fallback when it refuses) runs its wrapped argv and sees the session home as AW_SINDRI_HOME, never the real one; anything else runs as built.</summary>
    private async ValueTask<Result<Confined>> Confine(LaunchRequest request, IReadOnlyList<string> argv, Dictionary<string, string> env, SessionId? session, CancellationToken cancellationToken)
    {
        if (parts.Sandbox is not { } sandbox)
        {
            return new Confined(argv, env, Summary: null);
        }

        var prepared = await sandbox.Prepare(request, argv, session, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        env["AW_SINDRI_HOME"] = prepared.Value.SessionHome;
        env["AW_SINDRI_LAUNCH_SECRET_FILE"] = SessionHome.SecretFile(prepared.Value.SessionHome);   // the file moved into the home: the launcher's own directory isn't bound
        return new Confined(prepared.Value.Argv, env, prepared.Value.Summary, prepared.Value.RunArea);
    }

    /// <summary>Releases the launch's sandbox (egress, session home, denials) once its session is over or its start failed; a no-op for a launch with none, and a failure never fails the stop.</summary>
    private async ValueTask ReleaseSandbox(LaunchId launch)
    {
        if (parts.Sandbox is not { } sandbox)
        {
            return;
        }

        try
        {
            await sandbox.Release(launch, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // what the release left behind is the crash sweep's; the stop itself went through
        }
    }
}
