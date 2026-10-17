namespace Aw.Sindri.Core.Onyx;

/// <summary>Asks the Onyx on this machine whether it is ready, and which release it runs.</summary>
public interface IOnyxHealth
{
    /// <summary>Probes `http://127.0.0.1:&lt;port&gt;`: the health route first, the version route only once health answers. Never throws for a server that isn't there, refuses, hangs or answers garbage; each is "not healthy" (or a null version). Only the caller's own cancellation is thrown.</summary>
    /// <param name="port">The loopback port nginx is published on.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>What it found.</returns>
    public ValueTask<OnyxProbe> Probe(int port, CancellationToken cancellationToken);
}
