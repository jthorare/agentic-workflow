namespace Aw.Sindri.Core.Commands;

/// <summary>
/// The process-wide command gate (API spec §2.1) as a slice sees it: a handler registered without the gate (AddUngatedCommand) takes it
/// itself, around its writes only, and holds it for as long as the returned hold lives.
/// </summary>
public interface ICommandGate
{
    /// <summary>Waits for the gate for up to 5 s, as a gated command does.</summary>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <returns>The hold (dispose it to leave the gate), or null when the limit passed first (the caller answers SND-API-009).</returns>
    public ValueTask<IDisposable?> Enter(CancellationToken cancellationToken);
}
