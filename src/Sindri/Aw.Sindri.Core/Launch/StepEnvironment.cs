namespace Aw.Sindri.Core.Launch;

/// <summary>The run environment an unattended Step's session gets through <see cref="ISessionHost.Start"/> (scoping-station spec W11, L-17(b)).</summary>
public static class StepEnvironment
{
    /// <summary>The variable naming the instance address.</summary>
    public const string Instance = "AW_SINDRI_INSTANCE";

    /// <summary>The variable naming the Step directory the agent reads its facts from and writes its outputs to.</summary>
    public const string RunDirectory = "AW_SINDRI_RUN_DIR";

    /// <summary>The variables for <paramref name="request"/>: the instance for an unattended Step, and the run directory too when the Step names one (its policy's writable path, else the request's worktree), none for a human open.</summary>
    /// <param name="request">The launch.</param>
    /// <returns>Name to value.</returns>
    public static IReadOnlyDictionary<string, string> For(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, string> env = new(StringComparer.Ordinal);
        if (request.Step is { } step && request.Instance is { } instance)
        {
            env[Instance] = instance.Value;
            var directory = step.Policy?.WritablePath ?? request.Worktree;
            if (directory is not null)
            {
                env[RunDirectory] = directory;
            }
        }

        return env;
    }
}
