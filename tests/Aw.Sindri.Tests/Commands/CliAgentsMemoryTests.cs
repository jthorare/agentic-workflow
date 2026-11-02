using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri agents' prints a sandboxed session's memory in MiB below a tenth of a GiB, so a small session doesn't read as "using nothing" (#1555).</summary>
public sealed class CliAgentsMemoryTests : IDisposable
{
    private const long Gib = 1L << 30;
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_small_session_shows_its_memory_in_MiB()
    {
        var line = await SandboxedLine(used: 2_473_984, limit: 6_712_249_548);

        Assert.EndsWith("  sandboxed · mem 2.4 MiB/6.3 GiB", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_at_a_tenth_of_a_GiB_or_more_keeps_GiB()
    {
        var line = await SandboxedLine(used: 107_374_183, limit: 12 * Gib);

        Assert.EndsWith("  sandboxed · mem 0.1/12 GiB", line, StringComparison.Ordinal);
    }

    private async Task<string> SandboxedLine(long used, long limit)
    {
        var s = SessionsRig.Claude();
        var row = new SessionRow($"{s.Ref}", s.Ref.Provider, "Claude Code", s.Ref.Id, s.IdKnown, s.State, s.Reason is { } r ? new SessionAsking(r, s.Tool, s.Since) : null,
            StateSource.Hook, Estimated: false, s.Repo, Worktree: null, new SessionHostRow(s.Host.Kind, s.Host.Label, SocketKind: null, AttachedClients: null, Terminal: null), Lane: null, s.LaunchId, s.StartedAt, s.Since, SessionFocus.Pending)
        {
            Sandbox = new SessionSandbox(SessionSandboxState.On, Attended: false, SandboxData.Folder, new SessionMemory(used, limit)),
        };
        var list = new SessionList([row], [], [], new SessionCounts(1, 0, 1), Empty: null, SessionData.T0);
        var body = Encoding.UTF8.GetString(AwJson.Serialize(list, SliceHarness.Json));
        var verbs = new AgentsVerbs(new ControlPlaneClient(_up, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: new Api(body)),
            new Output(_out, _error, SliceHarness.Json));

        _ = await verbs.Run([], Token);

        return _out.ToString().ReplaceLineEndings("\n").Split('\n')[1];
    }

    private sealed class Api(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
