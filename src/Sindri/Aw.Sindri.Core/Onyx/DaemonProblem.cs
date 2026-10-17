namespace Aw.Sindri.Core.Onyx;

/// <summary>Why the Docker daemon didn't answer, as far as the docker CLI's own words say.</summary>
public enum DaemonProblem
{
    /// <summary>The daemon answers (nothing is wrong).</summary>
    None,

    /// <summary>It doesn't answer and nothing says why: Docker isn't running.</summary>
    NotRunning,

    /// <summary>It runs, but this user may not open its socket (permission denied).</summary>
    PermissionDenied,

    /// <summary>The selected docker context doesn't exist.</summary>
    ContextMissing,

    /// <summary>It didn't answer within the whole budget (the first try and the retry): it may be starting, as Docker Desktop does when it wakes from Resource Saver.</summary>
    Starting,
}
