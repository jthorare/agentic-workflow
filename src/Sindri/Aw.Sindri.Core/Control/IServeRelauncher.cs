using Aw.Kernel;

namespace Aw.Sindri.Core.Control;

/// <summary>Starts the installed build's 'sindri serve' once this process has exited (restart onto a newer build, #3052).</summary>
public interface IServeRelauncher
{
    /// <summary>
    /// Starts a detached helper that waits for this process to exit (at most 60 seconds), then starts 'bin/cs/current/sindri serve'. It never uses this process's own path: that is the old build.
    /// </summary>
    /// <returns>Unit when the helper runs; SND-BUILD-003 when it can't be started (this process isn't in the install layout, or /bin/sh didn't start).</returns>
    public Result<Unit> Schedule();
}
