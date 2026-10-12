namespace Aw.Sindri.Core.Control;

/// <summary>Asks the running control plane to stop (port; the host's application lifetime).</summary>
public interface IShutdown
{
    /// <summary>Requests a graceful stop; returns at once.</summary>
    public void Request();

    /// <summary>Cancelled once a stop is requested.</summary>
    public CancellationToken Stopping { get; }
}
