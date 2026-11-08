using System.Globalization;
using System.Net;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The one way a data block reads the web: only hosts on the allowlist, never loopback, a redirect only to the same host
/// (at most 3 hops, followed here: build the client with AllowAutoRedirect off), and no body past 1 MB.
/// </summary>
internal static class SafeHttp
{
    /// <summary>The biggest body a call may read.</summary>
    public const int MaxBodyBytes = 1_000_000;

    private const int MaxHops = 3;

    /// <summary>Sends one call under the rules above.</summary>
    /// <param name="http">The client; it must not follow redirects itself.</param>
    /// <param name="allowedHosts">The hosts the profile allows.</param>
    /// <param name="call">What to send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The body text, or the reason the block must fail.</returns>
    public static async ValueTask<Result<string>> Send(HttpClient http, IReadOnlySet<string> allowedHosts, SafeCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(allowedHosts);
        ArgumentNullException.ThrowIfNull(call);
        var origin = call.Uri.Host;
        var target = call.Uri;
        for (var hop = 0; hop <= MaxHops; hop++)
        {
            if (Refused(target, allowedHosts) is { } refused)
            {
                return refused;
            }

            var step = await OneHop(http, call with { Uri = target }, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.Body is { } text)
            {
                return text;
            }

            target = step.Value.Next ?? target;
            if (!string.Equals(target.Host, origin, StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"{origin} redirected to {target.Host}, which is a different host; the call is refused");
            }
        }

        return Fail($"{origin} redirected more than {MaxHops.ToString(CultureInfo.InvariantCulture)} times");
    }

    // The body, or (for a redirect) the place it points to.
    private static async ValueTask<Result<(string? Body, Uri? Next)>> OneHop(HttpClient http, SafeCall call, CancellationToken cancellationToken)
    {
        var host = call.Uri.Host;
        using var request = new HttpRequestMessage(call.Method, call.Uri);
        if (call.JsonBody is not null)
        {
            request.Content = new StringContent(call.JsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                return (null, location.IsAbsoluteUri ? location : new Uri(call.Uri, location));
            }

            if (!response.IsSuccessStatusCode)
            {
                return Fail($"{host} answered {((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)}").Error;
            }

            var body = await ReadCapped(response, host, cancellationToken).ConfigureAwait(false);
            if (body.IsError)
            {
                return body.Error;
            }

            return (body.Value, null);
        }
        catch (HttpRequestException ex)
        {
            return Fail($"{host} could not be reached ({ex.Message})").Error;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail($"{host} did not answer in time").Error;
        }
    }

    private static async ValueTask<Result<string>> ReadCapped(HttpResponseMessage response, string host, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxBodyBytes)
        {
            return Fail($"{host} sent more than 1 MB; the call is refused");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxBodyBytes)
                {
                    return Fail($"{host} sent more than 1 MB; the call is refused");
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
    }

    private static Error? Refused(Uri uri, IReadOnlySet<string> allowedHosts)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("https" or "http"))
        {
            return Fail($"'{uri}' is not an http or https URL").Error;
        }

        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)))
        {
            return Fail($"{host} is a loopback address; the call is refused").Error;
        }

        return allowedHosts.Contains(host) ? null : Fail($"{host} is not on the host allowlist in the profile; add it there to read it").Error;
    }

    private static Result<string> Fail(string why) => SindriErrors.GraphBlockFailed.With(why);
}
