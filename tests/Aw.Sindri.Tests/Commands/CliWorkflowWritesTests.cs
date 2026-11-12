using System.Net;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri wf create|edit|activate|pause|delete': each posts to its route and prints one line; a refusal prints its code and exits 2.</summary>
public sealed class CliWorkflowWritesTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("drift-check");

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    private WfWriteVerbs Verbs(FakeControlPlaneProbe probe, Api api, Error? down = null)
    {
        var client = new ControlPlaneClient(probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api);
        return new WfWriteVerbs(new RecipeDesk(client, TextReader.Null, _ => Task.FromResult(down)), new Output(_out, _error, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_create_posts_the_slug_and_template_and_prints_the_revision()
    {
        var api = new Api(HttpStatusCode.OK, Body(new CreateWorkflowResponse(WorkflowId.From("WF-00000000000000000000000000000001"), _slug, 1, "abc")));

        var code = await Verbs(_up, api).Create(["drift-check", "--template", "manual-notify"], Token);

        Assert.Equal((0, "POST", "/sindri/workflows"), (code, api.Method, api.Path));
        Assert.Contains("\"template\":\"manual-notify\"", api.Sent, StringComparison.Ordinal);
        Assert.Equal("drift-check created at revision 1 (off)\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_edit_with_ops_posts_the_base_revision_and_the_ops()
    {
        var api = new Api(HttpStatusCode.OK, Body(new ApplyWorkflowOpsResponse(_slug, 3, "abc")));

        var code = await Verbs(_up, api).Edit(["drift-check", "--base", "2", "--ops", "[{\"op\":\"rename\",\"node\":\"a\",\"to\":\"b\"}]"], Token);

        Assert.Equal((0, "POST", "/sindri/workflows/drift-check/ops"), (code, api.Method, api.Path));
        Assert.Contains("\"baseRevision\":2", api.Sent, StringComparison.Ordinal);
        Assert.Equal("drift-check is now at revision 3\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_edit_needs_a_base_revision_and_exactly_one_source()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var noBase = await Verbs(_up, api).Edit(["drift-check", "--ops", "[]"], Token);
        var noSource = await Verbs(_up, api).Edit(["drift-check", "--base", "1"], Token);

        Assert.Equal((2, 2, 0), (noBase, noSource, api.Calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_base_revision_prints_the_servers_conflict_and_exits_2()
    {
        var api = new Api(HttpStatusCode.Conflict, "{\"type\":\"about:blank\",\"title\":\"SND-GRAPH-007\",\"status\":409,\"detail\":\"This workflow is at revision 5\",\"currentRevision\":5}");

        var code = await Verbs(_up, api).Edit(["drift-check", "--base", "2", "--ops", "[]"], Token);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-GRAPH-007 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_activate_and_pause_from_a_session_print_the_servers_refusal_and_exit_2()
    {
        var api = new Api(HttpStatusCode.Forbidden, "{\"type\":\"about:blank\",\"title\":\"SND-AUTH-002\",\"status\":403,\"detail\":\"Activating a workflow is the owner's to do; a session can't\"}");

        var activate = await Verbs(_up, api).Activate(["drift-check"], Token);
        var pause = await Verbs(_up, api).Pause(["drift-check"], Token);

        Assert.Equal((2, 2), (activate, pause));
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_activate_sends_the_revision_and_pause_and_delete_use_their_routes()
    {
        var activate = new Api(HttpStatusCode.OK, Body(new ActivateWorkflowResponse(_slug, 2, "abc")));
        var pause = new Api(HttpStatusCode.OK, Body(new PauseWorkflowResponse(_slug, 2)));
        var delete = new Api(HttpStatusCode.OK, Body(new DeleteWorkflowResponse(_slug, DateTimeOffset.UnixEpoch)));

        _ = await Verbs(_up, activate).Activate(["drift-check", "--revision", "2"], Token);
        _ = await Verbs(_up, pause).Pause(["drift-check"], Token);
        _ = await Verbs(_up, delete).Delete(["drift-check"], Token);

        Assert.Equal(("POST", "/sindri/workflows/drift-check/activate"), (activate.Method, activate.Path));
        Assert.Contains("\"revision\":2", activate.Sent, StringComparison.Ordinal);
        Assert.Equal(("POST", "/sindri/workflows/drift-check/pause"), (pause.Method, pause.Path));
        Assert.Equal(("DELETE", "/sindri/workflows/drift-check"), (delete.Method, delete.Path));
        Assert.Equal("drift-check is on at revision 2\ndrift-check is off\ndrift-check is archived\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_writes_exit_1_when_serve_cannot_start_and_call_nothing()
    {
        var api = new Api(HttpStatusCode.OK, "{}");
        var down = Core.SindriErrors.NotRunning.With("not running");

        var code = await Verbs(new FakeControlPlaneProbe(), api, down).Pause(["drift-check"], Token);

        Assert.Equal((1, 0), (code, api.Calls));
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Sent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method.Method;
            Path = request.RequestUri?.AbsolutePath;
            Sent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") };
        }
    }
}
