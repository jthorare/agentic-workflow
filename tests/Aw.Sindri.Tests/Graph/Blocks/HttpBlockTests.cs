using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class HttpBlockTests
{
    private static HttpBlock Block(FlowFakes.ScriptedHandler handler, params string[] hosts) => new(new GraphHttpClient(handler, FlowFakes.Hosts(hosts)));

    private static Task<Kernel.Result<BlockResult>> Run(HttpBlock block, JsonObject config) => block.Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task A_host_off_the_allowlist_a_loopback_address_and_a_cross_host_redirect_are_refused()
    {
        var handler = new FlowFakes.ScriptedHandler(request => request.RequestUri!.Host == "api.example.com"
            ? FlowFakes.ScriptedHandler.Redirect("https://evil.example.net/steal")
            : FlowFakes.ScriptedHandler.Json("{}"));
        var block = Block(handler, "api.example.com", "localhost", "127.0.0.1");

        var offList = await Run(block, new JsonObject { ["url"] = "https://other.example.com/x" });
        var loopbackName = await Run(block, new JsonObject { ["url"] = "https://localhost/x" });
        var loopbackIp = await Run(block, new JsonObject { ["url"] = "https://127.0.0.1/x" });
        var v6 = await Run(block, new JsonObject { ["url"] = "https://[::1]/x" });
        var redirected = await Run(block, new JsonObject { ["url"] = "https://api.example.com/x" });

        Assert.Equal(SindriErrors.GraphHttpRefused.Code, offList.Error.Code);
        Assert.Contains("workflow.http.hosts", offList.Error.Message, StringComparison.Ordinal);
        Assert.Equal(SindriErrors.GraphHttpRefused.Code, loopbackName.Error.Code);
        Assert.Equal(SindriErrors.GraphHttpRefused.Code, loopbackIp.Error.Code);
        Assert.Equal(SindriErrors.GraphHttpRefused.Code, v6.Error.Code);
        Assert.Equal(SindriErrors.GraphHttpRefused.Code, redirected.Error.Code);
        Assert.Contains("evil.example.net", redirected.Error.Message, StringComparison.Ordinal);
        Assert.Equal([new Uri("https://api.example.com/x")], handler.Seen.Select(static s => s.Url));   // nothing else was ever requested
    }

    [Fact]
    public async Task Plain_http_user_info_and_credential_headers_are_refused_before_anything_is_sent()
    {
        var handler = new FlowFakes.ScriptedHandler(_ => FlowFakes.ScriptedHandler.Json("{}"));
        var block = Block(handler, "api.example.com");

        var plain = await Run(block, new JsonObject { ["url"] = "http://api.example.com/x" });
        var userInfo = await Run(block, new JsonObject { ["url"] = "https://user" + ":pw@api.example.com/x" });
        var credential = await Run(block, new JsonObject { ["url"] = "https://api.example.com/x", ["headers"] = new JsonObject { ["Authorization"] = "placeholder" } });
        var notAUrl = await Run(block, new JsonObject { ["url"] = "api.example.com/x" });

        Assert.Equal(SindriErrors.GraphHttpRefused.Code, plain.Error.Code);
        Assert.Equal(SindriErrors.GraphHttpRefused.Code, userInfo.Error.Code);
        Assert.Equal(SindriErrors.GraphHttpRefused.Code, credential.Error.Code);
        Assert.True(notAUrl.IsError);
        Assert.Empty(handler.Seen);
    }

    [Fact]
    public async Task A_redirect_to_the_same_host_is_followed_but_only_a_few_times()
    {
        var hops = 0;
        var handler = new FlowFakes.ScriptedHandler(request => request.RequestUri!.AbsolutePath == "/loop"
            ? FlowFakes.ScriptedHandler.Redirect(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://api.example.com/loop{++hops}"))
            : request.RequestUri.AbsolutePath == "/start" ? FlowFakes.ScriptedHandler.Redirect("https://api.example.com/done") : FlowFakes.ScriptedHandler.Json("""{"ok":true}"""));
        var block = Block(handler, "api.example.com");

        var followed = await Run(block, new JsonObject { ["url"] = "https://api.example.com/start" });
        var looping = await Run(new HttpBlock(new GraphHttpClient(new FlowFakes.ScriptedHandler(_ => FlowFakes.ScriptedHandler.Redirect("https://api.example.com/again")), FlowFakes.Hosts("api.example.com"))),
            new JsonObject { ["url"] = "https://api.example.com/again" });

        Assert.True(followed.Value.Outputs["body"]!["ok"]!.GetValue<bool>());
        Assert.Equal(SindriErrors.GraphHttpFailed.Code, looping.Error.Code);
    }

    [Fact]
    public async Task A_body_over_one_megabyte_fails_the_node()
    {
        var big = new string('x', GraphHttpClient.MaxBodyBytes + 1);
        var block = Block(new FlowFakes.ScriptedHandler(_ => FlowFakes.ScriptedHandler.Json(big)), "api.example.com");

        var result = await Run(block, new JsonObject { ["url"] = "https://api.example.com/x" });

        Assert.Equal(SindriErrors.GraphHttpFailed.Code, result.Error.Code);
        Assert.Contains("1 MB", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_streamed_body_with_no_length_over_one_megabyte_fails_the_node_and_stops_reading_near_the_cap()
    {
        var endless = new EndlessStream();
        var block = Block(new FlowFakes.ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(endless) }), "api.example.com");

        var result = await Run(block, new JsonObject { ["url"] = "https://api.example.com/x" });

        Assert.Equal(SindriErrors.GraphHttpFailed.Code, result.Error.Code);
        Assert.Contains("1 MB", result.Error.Message, StringComparison.Ordinal);
        Assert.InRange(endless.BytesRead, GraphHttpClient.MaxBodyBytes + 1, GraphHttpClient.MaxBodyBytes + (64 * 1024));   // one chunk past the cap, never the whole (endless) stream
    }

    /// <summary>A non-seekable stream with no end: no Content-Length, and a read that never stops on its own.</summary>
    private sealed class EndlessStream : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(count, 4096);
            Array.Fill(buffer, (byte)'x', offset, n);
            BytesRead += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = Math.Min(buffer.Length, 4096);
            buffer.Span[..n].Fill((byte)'x');
            BytesRead += n;
            return ValueTask.FromResult(n);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task The_status_text_and_json_body_come_back_and_a_status_outside_expect_fails_the_node()
    {
        var block = Block(new FlowFakes.ScriptedHandler(request => request.Method == HttpMethod.Post
            ? FlowFakes.ScriptedHandler.Json("""{"id":7}""", HttpStatusCode.Created)
            : FlowFakes.ScriptedHandler.Json("""{"error":"gone"}""", HttpStatusCode.NotFound)), "api.example.com");

        var created = await Run(block, new JsonObject { ["url"] = "https://api.example.com/items", ["method"] = "POST", ["body"] = new JsonObject { ["name"] = "n" }, ["expect"] = "200,201" });
        var missing = await Run(block, new JsonObject { ["url"] = "https://api.example.com/items/9" });
        var expected404 = await Run(block, new JsonObject { ["url"] = "https://api.example.com/items/9", ["expect"] = "404" });

        Assert.Equal(201, created.Value.Outputs["status"]!.GetValue<int>());
        Assert.Equal(7, created.Value.Outputs["body"]!["id"]!.GetValue<int>());
        Assert.Contains("404", missing.Error.Message, StringComparison.Ordinal);
        Assert.Contains("200-299", missing.Error.Message, StringComparison.Ordinal);
        Assert.Equal(404, expected404.Value.Outputs["status"]!.GetValue<int>());
    }

    [Fact]
    public async Task An_answer_that_is_not_json_gives_text_and_no_body()
    {
        var block = new HttpBlock(new FlowFakes.ScriptedHttpPort(new GraphHttpResponse(200, "plain words")));

        var result = await Run(block, new JsonObject { ["url"] = "https://api.example.com/x" });

        Assert.Equal("plain words", result.Value.Outputs["text"]!.GetValue<string>());
        Assert.Null(result.Value.Outputs["body"]);
    }

    [Fact]
    public async Task The_request_the_port_gets_has_the_method_headers_and_json_body()
    {
        var port = new FlowFakes.ScriptedHttpPort();

        _ = await Run(new HttpBlock(port), new JsonObject { ["url"] = "https://api.example.com/x", ["method"] = "put", ["headers"] = new JsonObject { ["X-Trace"] = "abc", ["N"] = 3 }, ["body"] = new JsonObject { ["a"] = 1 } });
        var bad = await Run(new HttpBlock(port), new JsonObject { ["url"] = "https://api.example.com/x", ["method"] = "TRACE" });

        var sent = Assert.Single(port.Sent);
        Assert.Equal(("PUT", "abc", "3", """{"a":1}"""), (sent.Method, sent.Headers["X-Trace"], sent.Headers["N"], sent.Body));
        Assert.Contains("method", bad.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_block_takes_a_url_and_optional_method_headers_body_and_expect()
    {
        var block = new HttpBlock(new FlowFakes.ScriptedHttpPort());

        Assert.Equal("http", block.Type);
        Assert.Empty(block.Config.Check(new JsonObject { ["url"] = "https://api.example.com" }));
        Assert.Contains(block.Config.Check([]), static p => p.Field == "url");
        Assert.Equal(["body", "status", "text"], block.OutputsFor([]).Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_body_that_stalls_after_the_headers_fails_within_the_request_budget()
    {
        var handler = new FlowFakes.ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledBody()) });
        var block = new HttpBlock(new GraphHttpClient(handler, FlowFakes.Hosts("api.example.com"), TimeSpan.FromMilliseconds(200)));
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        guard.CancelAfter(TimeSpan.FromSeconds(5));   // why: without the fix the read hangs; the guard turns that into a failure instead of a stuck run

        var result = await block.Execute(BlockFakes.Context(new JsonObject { ["url"] = "https://api.example.com/x" }), guard.Token).AsTask();

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphHttpFailed.Code, result.Error.Code);
        Assert.Contains("timed out", result.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>A response body that never delivers a byte until it is cancelled.</summary>
    private sealed class StalledBody : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
