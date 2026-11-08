using System.Globalization;
using System.Net.Http.Headers;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IConnectionProbe over an authenticated HTTPS GET. The error names the status, never the token or the body.</summary>
/// <param name="http">The HTTP client.</param>
internal sealed class HttpConnectionProbe(HttpClient http) : IConnectionProbe
{
    private readonly HttpClient _http = http;

    public async ValueTask<Result<Unit>> Probe(string url, TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return Unit.Value;
            }

            var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            return response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                ? SindriErrors.ConnectionNeedsSignIn.With($"the provider answered {status} to the probe")
                : SindriErrors.ConnectionRefreshFailed.With($"the provider answered {status} to the probe");
        }
        catch (HttpRequestException)
        {
            return SindriErrors.ConnectionRefreshFailed.With("the provider could not be reached");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ConnectionRefreshFailed.With("the provider did not answer in time");
        }
    }
}
