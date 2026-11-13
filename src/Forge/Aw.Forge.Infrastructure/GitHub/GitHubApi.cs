using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Credentials;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>
/// The HTTP side of the GitHub adapter: the API base per host, the bearer token (re-asked once on a 401), pagination and
/// the mapping of transport failures, 401, rate limits and 5xx to port errors. Everything else is the caller's to read.
/// </summary>
internal sealed class GitHubApi(HttpClient http, ICredentialSource credentials, ForgeHosts hosts)
{
    private int _sent;

    /// <summary>Requests that left this process.</summary>
    internal int Sent => Volatile.Read(ref _sent);

    internal ForgeHosts Hosts { get; } = hosts;

    internal static Uri Rest(string host, string path) =>
        new(string.Equals(host, "github.com", StringComparison.Ordinal) ? $"https://api.github.com{path}" : $"https://{host}/api/v3{path}");

    internal static Uri GraphQl(string host) =>
        new(string.Equals(host, "github.com", StringComparison.Ordinal) ? "https://api.github.com/graphql" : $"https://{host}/api/graphql");

    internal Error? Guard(string host) =>
        Hosts.Allows(host) ? null : ForgeErrors.HostNotAllowed.With($"host '{host}' is not in the forge hosts");

    internal ValueTask<Result<GitHubReply>> Call(string host, HttpMethod method, string path, byte[]? body, CancellationToken cancellationToken) =>
        Send(host, new Wire(method, Rest(host, path), body), cancellationToken);

    /// <summary>Reads a file's bytes with GitHub's raw media type (no 1 MB limit): the reply's Raw holds them; JSON answers (an error, a directory) still parse.</summary>
    internal ValueTask<Result<GitHubReply>> CallRaw(string host, string path, CancellationToken cancellationToken) =>
        Send(host, new Wire(HttpMethod.Get, Rest(host, path), Body: null, Raw: true), cancellationToken);

    internal ValueTask<Result<GitHubReply>> Query(string host, byte[] body, CancellationToken cancellationToken) =>
        Send(host, new Wire(HttpMethod.Post, GraphQl(host), body), cancellationToken);

    /// <summary>Reads every page of a list endpoint into one list of elements.</summary>
    internal async ValueTask<Result<IReadOnlyList<JsonElement>>> All(string host, string path, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var url = Rest(host, path + (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "per_page=100");
        for (var page = 0; page < 20; page++)
        {
            var reply = await Send(host, new Wire(HttpMethod.Get, url, Body: null), cancellationToken).ConfigureAwait(false);
            if (reply.IsError)
            {
                return reply.Error;
            }

            if (!reply.Value.Ok)
            {
                return Fail(reply.Value);
            }

            items.AddRange(reply.Value.Body.Items());
            if (reply.Value.Next is not { } next || !SameOrigin(next, url))
            {
                break;
            }

            url = next;
        }

        return items;
    }

    /// <summary>
    /// Reads the pages of a list endpoint from <paramref name="path"/> (used as given), one reply per page, stopping after
    /// <paramref name="maxPages"/> or once <paramref name="enough"/> says so. the set's More is true when it stopped with a next page left.
    /// </summary>
    internal async ValueTask<Result<PageSet>> Pages(string host, string path, int maxPages, Func<IReadOnlyList<GitHubReply>, bool> enough, CancellationToken cancellationToken)
    {
        var replies = new List<GitHubReply>();
        var url = Rest(host, path);
        for (var page = 1; ; page++)
        {
            var reply = await Send(host, new Wire(HttpMethod.Get, url, Body: null), cancellationToken).ConfigureAwait(false);
            if (reply.IsError)
            {
                return reply.Error;
            }

            if (!reply.Value.Ok)
            {
                return Fail(reply.Value);
            }

            replies.Add(reply.Value);
            if (reply.Value.Next is not { } next || !SameOrigin(next, url))
            {
                return new PageSet(replies, More: false);
            }

            if (page >= maxPages || enough(replies))
            {
                return new PageSet(replies, More: true);
            }

            url = next;
        }
    }

    private static bool SameOrigin(Uri next, Uri current) =>
        string.Equals(next.Scheme, "https", StringComparison.Ordinal) && string.Equals(next.Host, current.Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps a non-2xx answer to the port error it means.</summary>
    internal static Error Fail(GitHubReply reply)
    {
        var message = reply.Body.Str("message");
        return reply.Status switch
        {
            404 => ForgeErrors.NotFound.With(message),
            403 => ForgeErrors.Refused.With(message),
            _ => ForgeErrors.Rejected.With(Inv.S($"{reply.Status}: {message}")),
        };
    }

    private async ValueTask<Result<GitHubReply>> Send(string host, Wire wire, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await credentials.Token(host, refresh: attempt > 0, cancellationToken).ConfigureAwait(false);
            if (token.IsError)
            {
                return token.Error;
            }

            var answered = await Once(wire, token.Value, cancellationToken).ConfigureAwait(false);
            if (answered.IsError || answered.Value.Status != (int)HttpStatusCode.Unauthorized)
            {
                return answered;
            }

            if (attempt > 0)
            {
                return ForgeErrors.Unauthorized.With("the forge rejected the credential");
            }
        }
    }

    private async ValueTask<Result<GitHubReply>> Once(Wire wire, string token, CancellationToken cancellationToken)
    {
        var (method, url, body, raw) = wire;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd(raw ? "application/vnd.github.raw" : "application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("agentic-workflow-forge");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        _ = Interlocked.Increment(ref _sent);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await Read(response, raw, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return ForgeErrors.Unavailable.With(ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ForgeErrors.Unavailable.With("the forge did not answer in time");
        }
    }

    private static async ValueTask<Result<GitHubReply>> Read(HttpResponseMessage response, bool raw, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status >= 500)
        {
            return ForgeErrors.Unavailable.With(Inv.S($"the forge answered {status}"));
        }

        if (IsRateLimited(response, status))
        {
            return ForgeErrors.RateLimited.With("the forge is rate limiting this credential");
        }

        string text;
        if (raw)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
            {
                return new GitHubReply(status, default, NextPage(response), bytes);
            }

            text = System.Text.Encoding.UTF8.GetString(bytes);
        }
        else
        {
            text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return new GitHubReply(status, ParseBody(text), NextPage(response));
    }

    // why: GitHub's secondary rate limit is a 403 with Retry-After and no X-RateLimit-Remaining: 0; read as a refusal it would skip the repo instead of stopping the pass
    private static bool IsRateLimited(HttpResponseMessage response, int status) =>
        status == 429 || (status == 403 && (response.Headers.RetryAfter is not null
            || (response.Headers.TryGetValues("X-RateLimit-Remaining", out var left) && left.FirstOrDefault() == "0")));

    private static JsonElement ParseBody(string text)
    {
        if (text.Length == 0)
        {
            return default;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static Uri? NextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var part in string.Join(',', values).Split(','))
        {
            if (part.Contains("rel=\"next\"", StringComparison.Ordinal))
            {
                var open = part.IndexOf('<', StringComparison.Ordinal);
                var close = part.IndexOf('>', StringComparison.Ordinal);
                if (open >= 0 && close > open && Uri.TryCreate(part[(open + 1)..close], UriKind.Absolute, out var next))
                {
                    return next;
                }
            }
        }

        return null;
    }
}
