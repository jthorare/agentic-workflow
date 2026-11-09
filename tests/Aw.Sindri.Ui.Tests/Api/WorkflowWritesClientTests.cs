using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's two workflow writes (PR 17 part 2a): method, path (the run id escaped as one segment), body, the parsed answer, the refusal's code, and one request per call.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowWritesClientTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StartWorkflowRun_posts_a_null_payload_to_the_runs_path_and_reads_the_answer()
    {
        var canned = new Canned("""{"run":"morning-check@1:4","status":"queued","reason":"busy"}""");

        var started = await Client(canned).StartWorkflowRun(_api, "morning-check", Token);

        var sent = Assert.Single(canned.Seen);
        Assert.Equal(("POST", "/sindri/workflows/morning-check/runs", "{\"payload\":null}"), (sent.Method, sent.Path, sent.Body));
        Assert.Equal(("morning-check@1:4", "queued", "busy"), (started.Value.Run, started.Value.Status, started.Value.Reason));
    }

    [Fact]
    public async Task StartWorkflowRun_answers_the_refusal_code_and_is_sent_once()
    {
        var canned = new Canned("""{"type":"about:blank","title":"SND-GRAPH-105","status":409,"detail":"'morning-check' has no active revision; activate one first","traceId":"x"}""", HttpStatusCode.Conflict);

        var started = await Client(canned).StartWorkflowRun(_api, "morning-check", Token);

        Assert.Equal("SND-GRAPH-105", started.Error.Code);
        _ = Assert.Single(canned.Seen);
    }

    [Fact]
    public async Task CancelWorkflowRun_posts_to_the_cancel_path_with_the_run_id_as_one_segment()
    {
        var canned = new Canned("""{"run":"morning-check@1:3"}""");

        var cancelled = await Client(canned).CancelWorkflowRun(_api, "morning-check@1:3", Token);

        var sent = Assert.Single(canned.Seen);
        Assert.Equal("POST", sent.Method);
        Assert.Equal("/sindri/workflow-runs/morning-check@1:3/cancel", Uri.UnescapeDataString(sent.Path));
        Assert.Equal("morning-check@1:3", cancelled.Value.Run);
    }

    [Fact]
    public async Task CancelWorkflowRun_answers_already_ended_by_its_code()
    {
        var canned = new Canned("""{"type":"about:blank","title":"SND-GRAPH-107","status":409,"detail":"the run already ended","traceId":"x"}""", HttpStatusCode.Conflict);

        var cancelled = await Client(canned).CancelWorkflowRun(_api, "morning-check@1:1", Token);

        Assert.Equal("SND-GRAPH-107", cancelled.Error.Code);
    }

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    private sealed class Canned(string body, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(string Method, string Path, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!; // why: HttpClient always sets the URI
            var sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Seen.Add((request.Method.Method, uri.AbsolutePath, sent));
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
