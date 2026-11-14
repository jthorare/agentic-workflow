namespace Aw.Sindri.Ui.Terminal;

/// <summary>What the terminal view's Stop and Interrupt call: Stop goes through the stop route, Interrupt through the open socket as the key C-c (no route).</summary>
internal sealed class PaneControl(ISessionStopper stopper, Func<Uri?> api, Func<CancellationToken, Task<bool>> interrupt) : ISessionControl
{
    /// <summary>Why the last stop failed, in words ("404: this Sindri has no stop route yet"); empty after a success.</summary>
    public string Failure { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task<bool> Stop(string sessionRef, CancellationToken cancellationToken)
    {
        var at = sessionRef.IndexOf('/', StringComparison.Ordinal);
        if (at <= 0 || api() is not { } url)
        {
            Failure = "Sindri's address is unknown";
            return false;
        }

        var status = await stopper.Stop(url, sessionRef[..at], sessionRef[(at + 1)..], cancellationToken).ConfigureAwait(true);
        Failure = status switch
        {
            >= 200 and < 300 => string.Empty,
            0 => "Sindri didn't answer",
            404 => "404: this Sindri has no stop route yet",
            _ => status.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return Failure.Length == 0;
    }

    /// <inheritdoc />
    public Task<bool> Interrupt(string sessionRef, CancellationToken cancellationToken) => interrupt(cancellationToken);
}
