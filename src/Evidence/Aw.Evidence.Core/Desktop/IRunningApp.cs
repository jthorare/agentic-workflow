namespace Aw.Evidence.Core.Desktop;

/// <summary>An app started by <see cref="IDesktopApps"/>; disposing stops it and everything it started.</summary>
public interface IRunningApp : IAsyncDisposable
{
    /// <summary>How the app got its display, as a line for the pack ('display: xvfb-run (virtual X server)').</summary>
    public string Display { get; }

    /// <summary>Whether the app has ended, and how.</summary>
    /// <returns>Null while it runs; else 'exited N' and the tail of its standard error.</returns>
    public string? Ended();
}
