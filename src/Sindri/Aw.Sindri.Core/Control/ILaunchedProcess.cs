namespace Aw.Sindri.Core.Control;

/// <summary>A control plane this process started.</summary>
public interface ILaunchedProcess : IDisposable
{
    /// <summary>Its process id.</summary>
    public int Pid { get; }

    /// <summary>The log file its output goes to.</summary>
    public string LogPath { get; }

    /// <summary>Whether it has exited (it refused to start, or crashed).</summary>
    public bool HasExited { get; }

    /// <summary>
    /// The last 'SND-' line in its log, or a note that there is none. Only lines written since this start count, not an
    /// earlier run's (a concurrent start writing to the same log would count too).
    /// </summary>
    /// <returns>The line.</returns>
    public string LastError();
}
