using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>The transport of <see cref="GitHubClient"/>: one GET, its status mapped to a value or an error, the rate limit, the body parsed.</summary>
internal sealed partial class GitHubClient
{
    private async ValueTask<Result<Step>> Fetch(Uri url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("agentic-workflow-sindri");
        _ = request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        if (_settings.Token is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await Interpret(response, url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            return ConnectorsErrors.Unreachable.With($"GitHub at {url.Host} could not be reached ({e.HttpRequestError}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectorsErrors.Unreachable.With($"GitHub at {url.Host} did not answer in time.");
        }
    }

    private async ValueTask<Result<Step>> Interpret(HttpResponseMessage response, Uri url, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status is >= 300 and < 400)
        {
            return ConnectorsErrors.RedirectRefused.With(string.Create(CultureInfo.InvariantCulture, $"GitHub at {url.Host} answered {status}; redirects are never followed."));
        }

        if (status == 200)
        {
            return await Parse(response, url, cancellationToken).ConfigureAwait(false);
        }

        if (IsRateLimit(response))
        {
            return new Step(Body: null, Next: null, RateLimitedUntil: RateLimitReset(response));
        }

        return status switch
        {
            401 => ConnectorsErrors.Unauthorized.With("GitHub rejected the token; check context.sources.github.token (a read-only token with Issues, Pull requests and Metadata read)."),
            403 => ConnectorsErrors.AccessDenied.With("GitHub refused the request; the token may lack read access to the repo."),
            404 => ConnectorsErrors.NotFound.With("GitHub has no such repo, or the token cannot see it."),
            _ => ConnectorsErrors.BadReply.With(string.Create(CultureInfo.InvariantCulture, $"GitHub answered {status}.")),
        };
    }

    private static async ValueTask<Result<Step>> Parse(HttpResponseMessage response, Uri url, CancellationToken cancellationToken)
    {
        var next = GitHubWire.NextLink(response);
        if (next is not null && !GitHubWire.SameOrigin(next, url))
        {
            return ConnectorsErrors.BadReply.With("GitHub's next page is on another host; it is not followed.");
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(body);
            return new Step(document.RootElement.Clone(), next, RateLimitedUntil: null);
        }
        catch (JsonException)
        {
            return ConnectorsErrors.BadReply.With("GitHub's reply was not valid JSON.");
        }
    }

    private static bool IsRateLimit(HttpResponseMessage response)
        => response.StatusCode == HttpStatusCode.TooManyRequests
            || (response.StatusCode == HttpStatusCode.Forbidden && (GitHubWire.Header(response, "x-ratelimit-remaining") == "0" || GitHubWire.Header(response, "retry-after") is not null));

    private DateTimeOffset RateLimitReset(HttpResponseMessage response)
    {
        var now = _clock.GetUtcNow();
        if (int.TryParse(GitHubWire.Header(response, "retry-after"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return now.AddSeconds(seconds);
        }

        return long.TryParse(GitHubWire.Header(response, "x-ratelimit-reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : now.AddSeconds(DefaultWaitSeconds);
    }

    private sealed record Step(JsonElement? Body, Uri? Next, DateTimeOffset? RateLimitedUntil);
}
