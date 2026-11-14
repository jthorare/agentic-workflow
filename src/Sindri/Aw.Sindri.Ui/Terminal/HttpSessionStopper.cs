using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>The real stopper: one POST with the human token, never retried.</summary>
internal sealed class HttpSessionStopper(HttpClient http, IHumanTokenSource token) : ISessionStopper
{
    /// <inheritdoc />
    public async Task<int> Stop(Uri api, string provider, string id, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api, $"/sindri/sessions/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(id)}/stop"));
            request.Headers.Add("X-Sindri-Caller", "human");
            if (await token.Read(cancellationToken).ConfigureAwait(false) is { } value)
            {
                request.Headers.Add("X-Sindri-Token", value);
            }

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return (int)response.StatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return 0;
        }
    }
}
