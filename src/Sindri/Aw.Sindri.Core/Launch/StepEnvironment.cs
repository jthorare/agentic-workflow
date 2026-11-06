using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>The run environment an unattended Step's session gets through <see cref="ISessionHost.Start"/> (scoping-station spec W11, L-17(b)).</summary>
public static class StepEnvironment
{
    /// <summary>The variable naming the instance address.</summary>
    public const string Instance = "AW_SINDRI_INSTANCE";

    /// <summary>The variable naming the Step directory the agent reads its facts from and writes its outputs to.</summary>
    public const string RunDirectory = "AW_SINDRI_RUN_DIR";

    /// <summary>
    /// The Step's run directory (sandbox spec SB17): $AW_SINDRI_HOME/workflow/runs/&lt;instance&gt;/&lt;state&gt;/&lt;launch&gt;/, one per launch so a relaunch starts empty.
    /// Without a Sindri home (a test launcher) it is the policy's writable path, else the request's worktree. Null for a human open, which has no Step.
    /// </summary>
    /// <param name="request">The launch.</param>
    /// <param name="sindriHome">Sindri's own home (never a session home), or null.</param>
    /// <returns>The absolute path, or null.</returns>
    public static string? RunDirectoryOf(LaunchRequest request, string? sindriHome)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Step is not { } step || request.Instance is not { } instance)
        {
            return null;
        }

        return sindriHome is not null
            ? RunDirectoryAt(WorkflowInstanceId.Of(instance), step.Ref.State, request.Launch, sindriHome)
            : step.Policy?.WritablePath ?? request.Worktree;
    }

    /// <summary>The same directory from its parts, for the caller that has no request yet (a skill launch's seed names it).</summary>
    /// <param name="workflow">The instance.</param>
    /// <param name="state">The Step's state name.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="sindriHome">Sindri's own home.</param>
    /// <returns>The absolute path.</returns>
    public static string RunDirectoryAt(WorkflowInstanceId workflow, string state, LaunchId launch, string sindriHome)
        => $"{sindriHome.TrimEnd('/')}/workflow/runs/{workflow.Value}/{state}/{launch.Value}";

    /// <summary>The variables for <paramref name="request"/>: the instance for an unattended Step, and its run directory when it has one (<see cref="RunDirectoryOf"/>), none for a human open.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="sindriHome">Sindri's own home, or null.</param>
    /// <returns>Name to value.</returns>
    public static IReadOnlyDictionary<string, string> For(LaunchRequest request, string? sindriHome)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, string> env = new(StringComparer.Ordinal);
        if (request.Step is not null && request.Instance is { } instance)
        {
            env[Instance] = instance.Value;
            if (RunDirectoryOf(request, sindriHome) is { } directory)
            {
                env[RunDirectory] = directory;
            }
        }

        return env;
    }
}
