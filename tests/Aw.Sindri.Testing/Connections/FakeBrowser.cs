using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Infrastructure.Connections;

namespace Aw.Sindri.Testing.Connections;

/// <summary>
/// IBrowserOpener that stands in for the owner: it GETs the consent URL and follows the provider's redirect to the loopback callback, so the
/// sign-in finishes without a window. A test can also make it open nothing.
/// </summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because IBrowserOpener is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeBrowser : IBrowserOpener, IDisposable
{
    private readonly HttpClient _http = new();

    /// <summary>Gets the URLs opened, in order.</summary>
    public List<Uri> Opened { get; } = [];

    /// <summary>Gets or sets a value indicating whether no browser can start (an agent session on a box with no display).</summary>
    public bool Unavailable { get; set; }

    /// <inheritdoc />
    public bool Open(Uri url)
    {
        Opened.Add(url);
        if (Unavailable)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var response = await _http.GetAsync(url, CancellationToken.None);
            }
            catch (HttpRequestException)
            {
                // the listener closed first (a timeout test): the sign-in's own result is what the test reads
            }
        }, CancellationToken.None);
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
