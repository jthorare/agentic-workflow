using System.Net;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.GetItem;
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
