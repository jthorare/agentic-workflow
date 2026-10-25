using System.Globalization;
using System.Net;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Cli;

/// <summary>A running driver, found through its directory: driver.json for where it answers and the secret file beside it, sent as X-Ui-Driver-Token.</summary>
internal sealed class DriverClient : IDisposable
{
    private static readonly Dictionary<HttpStatusCode, ErrorKind> _kinds = new()
    {
        [HttpStatusCode.BadRequest] = ErrorKind.Invalid,
        [HttpStatusCode.Unauthorized] = ErrorKind.Forbidden,
        [HttpStatusCode.NotFound] = ErrorKind.NotFound,
        [HttpStatusCode.RequestTimeout] = ErrorKind.Timeout,
    };

    private static readonly TimeSpan _pingBudget = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly Uri _origin;

    private DriverClient(HttpClient http, Uri origin)
    {
        _http = http;
        _origin = origin;
    }

    /// <summary>Reads the driver directory and connects.</summary>
    /// <param name="directory">The driver directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The client, or AW-UIDRIVER-006 when no driver.json or secret is there or they are unreadable.</returns>
    internal static async Task<Result<DriverClient>> OpenAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = await File.ReadAllBytesAsync(Path.Combine(directory, DriverEndpoint.EndpointFile), cancellationToken).ConfigureAwait(false);
            var secret = (await File.ReadAllTextAsync(Path.Combine(directory, DriverEndpoint.TokenFile), cancellationToken).ConfigureAwait(false)).Trim();
            var read = AwJson.Deserialize<DriverEndpoint>(endpoint, UiDriverJson.Options);
            if (read.IsError || read.Value.Schema != DriverEndpoint.SchemaName || !Uri.TryCreate(read.Value.Url, UriKind.Absolute, out var origin) || secret.Length == 0)
            {
                return UiDriverErrors.Unreachable.With($"{directory} does not hold a driver.json and driver.token of this version");
            }

#pragma warning disable CA2000 // the client owns the handler (disposeHandler) and the DriverClient disposes the client
            var http = new HttpClient(new SocketsHttpHandler { UseProxy = false }, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
#pragma warning restore CA2000
            http.DefaultRequestHeaders.Add(DriverEndpoint.TokenHeader, secret);
            return new DriverClient(http, origin);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return UiDriverErrors.Unreachable.With($"no driver in {directory}: {e.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Calls a verb.</summary>
    /// <param name="verb">The verb's name.</param>
    /// <param name="body">The request's JSON.</param>
    /// <param name="timeout">How long to wait for the whole answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer's bytes (JSON, or PNG for a screenshot), or the driver's failure, AW-UIDRIVER-004 when it does not answer within <paramref name="timeout"/>, or AW-UIDRIVER-006 when it is unreachable.</returns>
    internal async Task<Result<byte[]>> CallAsync(string verb, byte[] body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        try
        {
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var response = await _http.PostAsync(new Uri(_origin, "/v1/" + verb), content, budget.Token).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(budget.Token).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK ? bytes : FailureOf(response.StatusCode, bytes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UiDriverErrors.TimedOut.With($"the driver at {_origin} did not answer {verb} within {((int)timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)} ms");
        }
        catch (HttpRequestException e)
        {
            return UiDriverErrors.Unreachable.With($"the driver at {_origin} did not answer: {e.Message}");
        }
    }

    /// <summary>Asks the driver whether it answers.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when GET /v1/ping answered 200.</returns>
    internal async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_pingBudget);
        try
        {
            using var response = await _http.GetAsync(new Uri(_origin, "/v1/ping"), budget.Token).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static Error FailureOf(HttpStatusCode status, byte[] body)
    {
        var failure = AwJson.Deserialize<DriverFailure>(body, UiDriverJson.Options);
        if (failure.IsSuccess)
        {
            var kind = _kinds.GetValueOrDefault(status, ErrorKind.Unavailable);
            return new Error(failure.Value.Code, kind, failure.Value.Message);
        }

        return UiDriverErrors.Failed.With($"the driver answered {((int)status).ToString(CultureInfo.InvariantCulture)}: {Encoding.UTF8.GetString(body)}");
    }
}
