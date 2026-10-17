using System.Net;
using System.Text;

namespace Aw.Forge.Tests.GitHub;

/// <summary>An HttpMessageHandler that answers from a script and records every request. Never reaches a network.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly List<Func<HttpRequestMessage, HttpResponseMessage?>> _routes = [];
    private readonly List<(HttpMethod Method, Uri Url, string? Authorization)> _seen = [];

    internal IReadOnlyList<(HttpMethod Method, Uri Url, string? Authorization)> Seen => _seen;

    /// <summary>Answers requests whose path and query start with <paramref name="pathPrefix"/>.</summary>
    internal StubHandler On(HttpMethod method, string pathPrefix, HttpStatusCode status, string body = "", params (string Name, string Value)[] headers)
    {
        _routes.Add(request => request.Method == method && request.RequestUri!.PathAndQuery.StartsWith(pathPrefix, StringComparison.Ordinal)
            ? Reply(status, body, headers)
            : null);
        return this;
    }

    /// <summary>Answers each matching request from the queue, in order, repeating the last.</summary>
    internal StubHandler Sequence(HttpMethod method, string pathPrefix, params (HttpStatusCode Status, string Body)[] replies)
    {
        var next = 0;
        _routes.Add(request =>
        {
            if (request.Method != method || !request.RequestUri!.PathAndQuery.StartsWith(pathPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var (status, body) = replies[Math.Min(next++, replies.Length - 1)];
            return Reply(status, body, []);
        });
        return this;
    }

    /// <summary>Throws a transport failure for matching requests.</summary>
    internal StubHandler Break(HttpMethod method, string pathPrefix)
    {
        _routes.Add(request => request.Method == method && request.RequestUri!.PathAndQuery.StartsWith(pathPrefix, StringComparison.Ordinal)
            ? throw new HttpRequestException("connection refused")
            : null);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _seen.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
        foreach (var route in _routes)
        {
            if (route(request) is { } response)
            {
                return Task.FromResult(response);
            }
        }

        return Task.FromResult(Reply(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}", []));
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body, (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            _ = response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
}
