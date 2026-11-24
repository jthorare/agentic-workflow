using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Aw.Sindri.Tests.Messages;

/// <summary>A stand-in for Slack's Web API that never touches the network: it answers each request from a script and records the url and the Authorization header it saw.</summary>
internal sealed class SlackStubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<(Uri Url, string? Authorization)> Seen { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage TooMany(int retryAfterSeconds)
    {
        var response = Json("{\"ok\":false,\"error\":\"ratelimited\"}", HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds));
        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // why: the client always sets the url
        Seen.Add((request.RequestUri!, request.Headers.Authorization?.ToString()));
        return Task.FromResult(answer(request));
    }
}
