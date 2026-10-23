namespace Aw.Sindri.Core.Onyx;

/// <summary>Asks the Onyx on this machine whether it is ready, and which release it runs.</summary>
public interface IOnyxHealth
{
    /// <summary>Probes the origin (`http://127.0.0.1:&lt;port&gt;` for the managed stack, a cluster address for an attached one): the health route first, the version route only once health answers. Never throws for a server that isn't there, refuses, hangs or answers garbage; each is "not healthy" (or a null version). Only the caller's own cancellation is thrown.</summary>
    /// <param name="origin">Where Onyx answers; a port converts to its loopback origin.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>What it found.</returns>
    public ValueTask<OnyxProbe> Probe(OnyxOrigin origin, CancellationToken cancellationToken);
}
