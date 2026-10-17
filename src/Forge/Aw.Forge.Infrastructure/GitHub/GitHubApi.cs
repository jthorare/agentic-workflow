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
        Send(host, method, Rest(host, path), body, cancellationToken);

    internal ValueTask<Result<GitHubReply>> Query(string host, byte[] body, CancellationToken cancellationToken) =>
        Send(host, HttpMethod.Post, GraphQl(host), body, cancellationToken);

    /// <summary>Reads every page of a list endpoint into one list of elements.</summary>
    internal async ValueTask<Result<IReadOnlyList<JsonElement>>> All(string host, string path, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var url = Rest(host, path + (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "per_page=100");
        for (var page = 0; page < 20; page++)
        {
            var reply = await Send(host, HttpMethod.Get, url, body: null, cancellationToken).ConfigureAwait(false);
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

    private static bool SameOrigin(Uri next, Uri current) =>
        string.Equals(next.Scheme, "https", StringComparison.Ordinal) && string.Equals(next.Host, current.Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps a non-2xx answer to the port error it means.</summary>
    internal static Error Fail(GitHubReply reply)
    {
        var message = reply.Body.Str("message");
        return reply.Status switch
        {
            404 => ForgeErrors.NotFound.With(message),
            403 => ForgeErrors.Unauthorized.With(message),
            _ => ForgeErrors.Rejected.With(Inv.S($"{reply.Status}: {message}")),
        };
    }

    private async ValueTask<Result<GitHubReply>> Send(string host, HttpMethod method, Uri url, byte[]? body, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await credentials.Token(host, refresh: attempt > 0, cancellationToken).ConfigureAwait(false);
            if (token.IsError)
            {
                return token.Error;
            }

            var answered = await Once(method, url, body, token.Value, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<Result<GitHubReply>> Once(HttpMethod method, Uri url, byte[]? body, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
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
            return await Read(response, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<Result<GitHubReply>> Read(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status >= 500)
        {
            return ForgeErrors.Unavailable.With(Inv.S($"the forge answered {status}"));
        }

        if (status == 429 || (status == 403 && response.Headers.TryGetValues("X-RateLimit-Remaining", out var left) && left.FirstOrDefault() == "0"))
        {
            return ForgeErrors.RateLimited.With("the forge is rate limiting this credential");
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var body = default(JsonElement);
        if (text.Length > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                body = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                body = default;
            }
        }

        return new GitHubReply(status, body, NextPage(response));
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
