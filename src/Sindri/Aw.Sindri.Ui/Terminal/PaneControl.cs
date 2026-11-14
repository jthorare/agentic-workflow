namespace Aw.Sindri.Ui.Terminal;

/// <summary>What the terminal view's Stop and Interrupt call: Stop goes through the stop route, Interrupt through the open socket as the key C-c (no route).</summary>
internal sealed class PaneControl(ISessionStopper stopper, Func<Uri?> api, Func<CancellationToken, Task<bool>> interrupt) : ISessionControl
{
    /// <inheritdoc />
    public Task<bool> Stop(string sessionRef, CancellationToken cancellationToken)
    {
        var at = sessionRef.IndexOf('/', StringComparison.Ordinal);
        return at > 0 && api() is { } url ? stopper.Stop(url, sessionRef[..at], sessionRef[(at + 1)..], cancellationToken) : Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> Interrupt(string sessionRef, CancellationToken cancellationToken) => interrupt(cancellationToken);
}
