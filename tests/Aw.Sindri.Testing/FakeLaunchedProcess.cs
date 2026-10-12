using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>A launched control plane with a scripted exit and last error line.</summary>
/// <param name="pid">Its pid.</param>
/// <param name="lastError">What <see cref="LastError"/> answers.</param>
public sealed class FakeLaunchedProcess(int pid, string lastError) : ILaunchedProcess
{
    /// <inheritdoc/>
    public int Pid => pid;

    /// <inheritdoc/>
    public string LogPath => "/fake/logs/serve.log";

    /// <inheritdoc/>
    public bool HasExited { get; set; }

    /// <summary>Whether the caller disposed it.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public string LastError() => lastError;

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;
}
