using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri pr babysit &lt;url&gt;' and 'sindri docs sync &lt;url&gt;' (PR babysitter spec API16, §4.2): they build the hand start's facts and start the recipe through the start route, like 'workflow start'.</summary>
public sealed class HandStartCommandTests : IDisposable
{
    private const string Link = "https://github.com/acme/widgets/pull/42";
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly AsOf _fresh = new(5, 5, KnownInstants.Epoch, Rebuilding: false, Stalled: false);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Babysit_starts_the_babysitter_with_the_link_and_four_empty_facts_on_the_given_item()
    {
        var api = StartApi();

        var code = await Run(api, c => HandStartCommand.Babysit([Link, "--item", "ITM-1", "--json"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(0, code);
        var start = Started(api);
        Assert.Equal(("workflow.pr-babysitter", "ITM-1", false), (start.Recipe, start.Item, start.NextAttempt));
        Assert.Equal(
            ["fact:handoff={}", "fact:lineContext={}", "fact:phasePacket={}", "fact:pr=\"" + Link + "\"", "fact:workspace={}"],
            [.. start.Facts.OrderBy(f => f.Key.Value, StringComparer.Ordinal).Select(f => $"{f.Key.Value}={Encoding.UTF8.GetString(f.Value.Content.Span)}")]);
        Assert.DoesNotContain(api.Calls, c => string.Equals(c.Path, "/sindri/items", StringComparison.Ordinal));   // --item given: no item is looked up or created
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Docs_sync_starts_docs_sync_with_the_link_and_an_empty_phase_packet()
    {
        var api = StartApi();

        var code = await Run(api, c => HandStartCommand.DocsSync([Link, "--item", "ITM-1", "--json"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(0, code);
        var start = Started(api);
        Assert.Equal("workflow.docs-sync", start.Recipe);
        Assert.Equal(
            ["fact:phasePacket={}", "fact:pr=\"" + Link + "\""],
            [.. start.Facts.OrderBy(f => f.Key.Value, StringComparer.Ordinal).Select(f => $"{f.Key.Value}={Encoding.UTF8.GetString(f.Value.Content.Span)}")]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_link_is_sent_in_its_canonical_form_and_json_prints_the_raw_body()
    {
        var api = StartApi();

        var code = await Run(api, c => HandStartCommand.Babysit(["https://GitHub.com/acme/widgets/pull/42/files", "--item", "ITM-1", "--json"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(0, code);
        Assert.Contains("fact:pr=\"" + Link + "\"", Started(api).Facts.Select(f => $"{f.Key.Value}={Encoding.UTF8.GetString(f.Value.Content.Span)}"), StringComparer.Ordinal);
        using var body = JsonDocument.Parse(_out.ToString());
        Assert.True(body.RootElement.TryGetProperty("instance", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_item_it_uses_the_open_item_titled_with_the_link()
    {
        var api = StartApi().On("/sindri/items", HttpStatusCode.OK, new ListItemsResponse(
            [Summary(ItemId.From("ITM-00000000000000000000000000000009"), "something else"), Summary(A.FirstItemId, Link)], Empty: null, _fresh));

        var code = await Run(api, c => HandStartCommand.Babysit([Link, "--json"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(0, code);
        Assert.Equal(A.FirstItemId.Value, Started(api).Item);
        Assert.DoesNotContain(api.Calls, c => IsPost(c, "/sindri/items"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_item_and_without_a_match_it_creates_one_titled_with_the_link()
    {
        var api = StartApi()
            .On("/sindri/items", HttpStatusCode.OK, new ListItemsResponse([Summary(A.FirstItemId, "something else")], Empty: null, _fresh));
        var created = new ItemCreatedResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 7), ItemId.From("ITM-00000000000000000000000000000002"));
        api = api.OnPost("/sindri/items", HttpStatusCode.OK, created);

        var code = await Run(api, c => HandStartCommand.DocsSync([Link, "--json"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(0, code);
        var post = Assert.Single(api.Calls, c => IsPost(c, "/sindri/items"));
        Assert.Contains($"\"title\":\"{Link}\"", post.Body, StringComparison.Ordinal);
        Assert.Equal("ITM-00000000000000000000000000000002", Started(api).Item);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Text_output_is_the_instance_header_like_workflow_start()
    {
        var api = StartApi();

        var code = await Run(api, c => HandStartCommand.Babysit([Link, "--item", "ITM-1"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(0, code);
        Assert.StartsWith("ITM-1/step/1  workflow.step@", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not a link")]
    [InlineData("http://github.com/acme/widgets/pull/42")]
    [InlineData("https://github.com/acme/widgets/issues/42")]
    [InlineData("https://github.com/acme/widgets/pull/x")]
    public async Task A_bad_link_is_exit_2_with_a_fix_hint_and_no_call(string link)
    {
        var api = StartApi();

        var code = await Run(api, c => HandStartCommand.Babysit([link, "--item", "ITM-1"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("fix:", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData]
    [InlineData("--item", "ITM-1")]
    [InlineData(Link, "--item")]
    [InlineData(Link, "--bogus")]
    [InlineData(Link, "extra")]
    public async Task Missing_link_or_a_bad_flag_is_exit_2(params string[] args)
    {
        var api = StartApi();

        var code = await Run(api, c => HandStartCommand.DocsSync(args, c.Desk, c.Services, c.Output, Token));

        Assert.Equal(2, code);
        Assert.Contains(HandStartCommand.SyncUsage, _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_control_plane_that_is_not_running_is_exit_1()
    {
        var api = StartApi();
        var down = SindriErrors.NotRunning.With("not running (fix: start it with 'sindri')");

        var code = await Run(api, c => HandStartCommand.Babysit([Link, "--item", "ITM-1"], c.Desk with { EnsureRunning = _ => Task.FromResult<Kernel.Error?>(down) }, c.Services, c.Output, Token));

        Assert.Equal(1, code);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_start_is_exit_2()
    {
        var api = new RoutedApi().OnRaw("/sindri/workflow/instances", HttpStatusCode.Conflict, """{"type":"about:blank","title":"SND-WF-012","status":409,"detail":"refused"}""");

        var code = await Run(api, c => HandStartCommand.Babysit([Link, "--item", "ITM-1"], c.Desk, c.Services, c.Output, Token));

        Assert.Equal(2, code);
        Assert.Contains("SND-WF-012", _error.ToString(), StringComparison.Ordinal);
    }

    private static RoutedApi StartApi()
    {
        var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
        var address = InstanceAddress.From("ITM-1/step/1");
        var view = new InstanceView(address, recipe, "produce", Detail: null, KnownInstants.Epoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
        {
            Id = WorkflowInstanceId.Of(address),
        };
        return new RoutedApi().OnPost("/sindri/workflow/instances", HttpStatusCode.OK, new StartInstanceResponse(view, Existing: false, Finished: false));
    }

    private static StartInstanceRequest Started(RoutedApi api)
    {
        var call = Assert.Single(api.Calls, c => IsPost(c, "/sindri/workflow/instances"));
        return AwJson.Deserialize<StartInstanceRequest>(Encoding.UTF8.GetBytes(call.Body!), SliceHarness.Json).Value;
    }

    private static bool IsPost(Call call, string path) => string.Equals(call.Method, "POST", StringComparison.Ordinal) && string.Equals(call.Path, path, StringComparison.Ordinal);

    private static ItemSummary Summary(ItemId id, string title) =>
        new(id, title, Repo: null, ItemState.New, Step: null, KnownInstants.Epoch, PrimaryAction: null, Actions: [], Owner: null);

    private async Task<int> Run(RoutedApi api, Func<(RecipeDesk Desk, IServiceProvider Services, Output Output), Task<int>> verb)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var desk = new RecipeDesk(client, TextReader.Null, static _ => Task.FromResult<Kernel.Error?>(null));
        await using var services = new ServiceCollection().AddSingleton(TimeProvider.System).BuildServiceProvider();
        return await verb((desk, services, new Output(_out, _error, SliceHarness.Json)));
    }
}
