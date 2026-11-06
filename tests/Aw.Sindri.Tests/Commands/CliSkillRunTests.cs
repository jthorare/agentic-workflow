using System.Net;
using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri skill run' (skill flows spec §7): the item it makes for the trial does not stay open when the run is refused.</summary>
public sealed class CliSkillRunTests : IDisposable
{
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
    public async Task A_refused_run_closes_its_item_as_cancelled_and_exits_with_the_refusal()
    {
        var item = A.FirstItemId.Value;
        var api = new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7)
            .On("/sindri/items", HttpStatusCode.OK, new ItemCreatedResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 4), A.FirstItemId))
            .OnRaw("/sindri/skills/review/runs", HttpStatusCode.BadRequest, """{"title":"SND-CLI-003","detail":"block 'review' needs input 'pr'"}""")
            .On("/sindri/items/" + item, HttpStatusCode.OK, View(version: 1))
            .On("/sindri/items/" + item + "/close", HttpStatusCode.OK, new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 5));

        var code = await Verb(api).Run(["review"], Token);

        Assert.Equal(2, code);
        Assert.Contains("needs input 'pr'", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("""{"reason":"cancelled","itemVersion":1}""", api.Calls[^1].Body);
        Assert.Equal("/sindri/items/" + item + "/close", api.Calls[^1].Path);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_result_line_is_the_newest_skill_launchs_result_file()
    {
        var api = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage(
        [
            Started(1, """{"launchId":"LCH-1","skill":{"name":"review","resultFile":"/run/old/result-LCH-1.json"}}"""),
            Started(2, """{"launchId":"LCH-2"}"""),
            Started(3, """{"launchId":"LCH-3","skill":{"name":"review","resultFile":"/run/new/result-LCH-3.json"}}"""),
        ], Next: null));

        var path = await Verb(api).ResultFileOf(A.FirstItemId.Value, Token);

        Assert.Equal("/run/new/result-LCH-3.json", path);
        Assert.Equal("?item=" + A.FirstItemId.Value + "&type=launch.started", api.Calls[^1].Query);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_launch_with_a_result_file_or_an_unreadable_ledger_leaves_the_line_out()
    {
        var none = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage([Started(1, """{"launchId":"LCH-1"}""")], Next: null));
        var down = new RoutedApi().OnRaw("/sindri/ledger", HttpStatusCode.ServiceUnavailable, """{"title":"SND-API-011","detail":"views are behind"}""");

        Assert.Equal(string.Empty, await Verb(none).ResultFileOf(A.FirstItemId.Value, Token));
        Assert.Equal(string.Empty, await Verb(down).ResultFileOf(A.FirstItemId.Value, Token));
    }

    private static PublicEvent Started(long sequence, string payload) =>
        new(sequence, EventId.From(new Guid((int)sequence, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), "launch.started", 1, KnownInstants.Epoch, "service:host", "local", 0, A.FirstItemId,
            CorrelationId.From(new Guid(9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), CausationId: null, TraceId: null, JsonDocument.Parse(payload).RootElement);

    private static ItemView View(long version) =>
        new(A.FirstItemId, "Try the review block", Repo: null, ItemState.New, Step: null, KnownInstants.Epoch, PrimaryAction: null, Actions: [], Owner: null,
            version, "human:desk", WaitingOn: null, Instance: null, KnownInstants.Epoch, KnownInstants.Epoch, Body: null, Link: null, new Core.Launches.ItemLaunch(Modes: [], Core.Launch.AgentId.From("claude-code"), ChosenBy: "builtIn", Attach: null, Resumable: null, InFlight: null, LastStarted: null, Confirm: null, RepoPath: null, Blockers: []),
            new AsOf(5, 5, KnownInstants.Epoch, Rebuilding: false, Stalled: false));

    private SkillRunVerb Verb(RoutedApi api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new SkillRunVerb(client, new Output(_out, _error, SliceHarness.Json), _ => null, TimeProvider.System);
    }
}
