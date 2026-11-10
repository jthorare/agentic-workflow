using System.Net;
using System.Text;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri views rebuild' (plan unit D): posts RebuildViews, prints the sequence; a session gets the server's refusal.</summary>
public sealed class CliViewsRebuildTests : IDisposable
{
    private const string Refusal = "RebuildViews is a human decision. Ask the human to run it in a terminal or the Sindri app: sindri views rebuild";
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_views_rebuild_posts_as_human()
    {
        var accepted = new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 7);
        var api = new CliStubApi(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(accepted, SliceHarness.Json)));
        using var client = Client(api, human: true);

        var code = await Verbs(client).RebuildViews(Token);

        Assert.Equal((0, "views rebuild requested (sequence 7)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        var (route, headers) = Assert.Single(api.Calls);
        Assert.Equal(("/sindri/views/rebuild", "human"), (route, headers[SindriHeaders.Caller]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_prints_the_refusal_and_why_it_ran_as_one()
    {
        var problem = $$"""{"type":"about:blank","title":"SND-AUTH-002","status":403,"detail":"{{Refusal}}"}""";
        using var client = Client(new CliStubApi(HttpStatusCode.Forbidden, problem), human: false);

        var code = await Verbs(client).RebuildViews(Token);

        Assert.Equal(2, code);
        Assert.Equal($"SND-AUTH-002 {Refusal}\n", _error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Usage_and_the_catalog_name_the_same_verb()
    {
        var row = Assert.Single(ShellRows.All, r => r.Action.Value == "RebuildViews");

        Assert.Equal("sindri views rebuild", row.Cli.Text);
        Assert.Contains("sindri views rebuild [--no-start]", Usage.Text, StringComparison.Ordinal);
    }

    private static ControlPlaneClient Client(CliStubApi api, bool human) =>
        new(_probe, SliceHarness.Json, CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), human ? InMemoryHumanTokenStore.WithToken() : new InMemoryHumanTokenStore(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);

    private Verbs Verbs(ControlPlaneClient client) =>
        new(client, _probe, new Output(_out, _error, SliceHarness.Json), new ServiceCollection().BuildServiceProvider());
}
