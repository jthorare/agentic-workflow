using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>#2862: the queue read sends the last tag as If-None-Match and a 304 keeps the view it has (GET /sindri/queue answers tag, ETag and 304 since #2859).</summary>
[Lane(TestLane.Unit)]
public sealed class QueueConditionalReadTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private static readonly Uri _other = new("http://127.0.0.1:7411/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (HttpStatusCode Code, string Body, string? ETag) Ok(string tag) =>
        (HttpStatusCode.OK, $$"""{"groups":[{"group":"queued","items":[],"empty":"Nothing is queued."}],"empty":null,"asOf":{"cursor":1,"ledgerHead":1,"projectedAt":null,"rebuilding":false,"stalled":false},"tag":"{{tag}}"}""", "\"" + tag + "\"");

    private static (HttpStatusCode Code, string Body, string? ETag) NotModified() => (HttpStatusCode.NotModified, string.Empty, null);

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public async Task The_second_queue_read_sends_the_first_tag_and_a_304_returns_the_same_view()
    {
        var script = new Script(Ok("t1"), NotModified());
        var client = Client(script);

        var first = await client.Queue(_api, Token);
        var second = await client.Queue(_api, Token);

        Assert.Equal([null, "\"t1\""], script.IfNoneMatch);
        Assert.True(second.IsSuccess, "a 304 is an answer, not an error");
        Assert.Same(first.Value, second.Value);
    }

    [Fact]
    public async Task A_200_with_a_new_tag_returns_the_new_view_and_the_next_read_sends_that_tag()
    {
        var script = new Script(Ok("t1"), Ok("t2"), NotModified());
        var client = Client(script);

        var first = await client.Queue(_api, Token);
        var second = await client.Queue(_api, Token);
        _ = await client.Queue(_api, Token);

        Assert.NotSame(first.Value, second.Value);
        Assert.Equal("t2", second.Value.Tag);
        Assert.Equal([null, "\"t1\"", "\"t2\""], script.IfNoneMatch);
    }

    [Fact]
    public async Task Another_api_url_sends_no_if_none_match()
    {
        var script = new Script(Ok("t1"), Ok("t9"));
        var client = Client(script);

        _ = await client.Queue(_api, Token);
        _ = await client.Queue(_other, Token);

        Assert.Equal([null, null], script.IfNoneMatch);
    }

    private sealed class Script((HttpStatusCode Code, string Body, string? ETag) first, params (HttpStatusCode Code, string Body, string? ETag)[] rest) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Code, string Body, string? ETag)> _answers = new([first, .. rest]);

        public List<string?> IfNoneMatch { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            IfNoneMatch.Add(request.Headers.TryGetValues("If-None-Match", out var values) ? string.Join(", ", values) : null);
            var (code, body, etag) = _answers.Dequeue();
            var response = new HttpResponseMessage(code);
            if (body.Length > 0)
            {
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            if (etag is not null)
            {
                response.Headers.ETag = new EntityTagHeaderValue(etag);
            }

            return Task.FromResult(response);
        }
    }
}
