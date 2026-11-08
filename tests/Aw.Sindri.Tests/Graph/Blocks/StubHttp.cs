using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>An HttpMessageHandler that answers from a function and records what it was asked. No network.</summary>
internal sealed class StubHttp(Func<HttpRequestMessage, string?, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<(string Method, Uri Uri, string? Body)> Calls { get; } = [];

    public HttpClient Client() => new(this, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(5) };

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Redirect(string to) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(to) } };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Calls.Add((request.Method.Method, request.RequestUri!, body));
        return answer(request, body);
    }
}
