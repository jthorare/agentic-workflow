using System.Net;
using System.Text;

namespace Aw.Judge.Tests.Providers;

/// <summary>An HttpMessageHandler that records each request (with its body) and answers from a function.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly List<(HttpRequestMessage Request, string Body)> _requests = [];

    public IReadOnlyList<(HttpRequestMessage Request, string Body)> Requests => _requests;

    public static FakeHttpHandler Answering(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add((request, body));
        return await respond(request, cancellationToken);
    }
}
