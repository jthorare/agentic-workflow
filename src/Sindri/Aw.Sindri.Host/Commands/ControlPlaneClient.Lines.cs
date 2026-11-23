using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>The NDJSON reads of the control plane's API (the repo scan, first-run spec §3.3).</summary>
internal sealed partial class ControlPlaneClient
{
    /// <summary>
    /// GET a route that answers NDJSON and read its lines as they arrive, blank lines dropped. A refusal before the answer starts (a problem answer, not running) arrives as one error, then the sequence ends;
    /// so does the sequence when the server closes the answer. No call timeout applies once the headers are in. Cancelling <paramref name="ct"/> ends it with the cancellation and closes the answer.
    /// </summary>
    /// <param name="route">The route under the base URL, e.g. '/sindri/setup/repos?root=~'.</param>
    /// <param name="ct">Ends the read.</param>
    /// <returns>The lines, or the refusal.</returns>
    public async IAsyncEnumerable<Result<string>> Lines(string route, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var opened = await Open(route, ct).ConfigureAwait(false);
        if (opened.IsError)
        {
            yield return opened.Error;
            yield break;
        }

        using var response = opened.Value;
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false));
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }
}
