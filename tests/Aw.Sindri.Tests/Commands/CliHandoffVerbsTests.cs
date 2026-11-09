using System.Net;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateHandoffItem;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri handoff send' (#2258 PR 12): the flags it parses, the one POST /sindri/handoff it makes and its exit codes.</summary>
public sealed class CliHandoffVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly List<string> _tempFiles = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        foreach (var file in _tempFiles)
        {
            File.Delete(file);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Send_posts_the_handoff_and_prints_the_item()
    {
        var api = Api();

        var code = await Verbs(api).Run(["send", "Fix", "the", "login", "timeout", "--source", "ci-bot", "--offer", "offer-7", "--branch", "fix/login", "--label", "bug"], Token);

        Assert.Equal((0, $"{A.FirstItemId.Value} handed off (offer offer-7, sequence 4)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        var call = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/handoff"), (call.Method, call.Path));
        var sent = JsonDocument.Parse(call.Body!).RootElement;
        Assert.Equal(("Fix the login timeout", "ci-bot", "offer-7", "fix/login"),
            (sent.GetProperty("title").GetString(), sent.GetProperty("source").GetString(), sent.GetProperty("offerKey").GetString(), sent.GetProperty("branch").GetString()));
        Assert.Equal("bug", sent.GetProperty("labels")[0].GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Source_defaults_to_local()
    {
        var api = Api();

        _ = await Verbs(api).Run(["send", "Try it"], Token);

        Assert.Equal("local", JsonDocument.Parse(Assert.Single(api.Calls).Body!).RootElement.GetProperty("source").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Json_prints_the_raw_answer()
    {
        var code = await Verbs(Api()).Run(["send", "Try it", "--json"], Token);

        Assert.Equal(0, code);
        Assert.Contains("\"itemId\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_link_with_a_label_splits_at_the_first_equals_before_the_scheme()
    {
        var api = Api();

        _ = await Verbs(api).Run(["send", "Try it", "--link", "Ticket=https://example.test/t?id=7"], Token);

        var link = JsonDocument.Parse(Assert.Single(api.Calls).Body!).RootElement.GetProperty("links")[0];
        Assert.Equal(("Ticket", "https://example.test/t?id=7"), (link.GetProperty("label").GetString(), link.GetProperty("url").GetString()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bare_url_is_its_own_label_even_when_it_has_a_query_equals()
    {
        var api = Api();

        _ = await Verbs(api).Run(["send", "Try it", "--link", "https://example.test/t?id=7"], Token);

        var link = JsonDocument.Parse(Assert.Single(api.Calls).Body!).RootElement.GetProperty("links")[0];
        Assert.Equal(("https://example.test/t?id=7", "https://example.test/t?id=7"), (link.GetProperty("label").GetString(), link.GetProperty("url").GetString()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Prompt_file_sends_the_files_text()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        await File.WriteAllTextAsync(path, "Run the suite twice.", Token);
        var api = Api();

        var code = await Verbs(api).Run(["send", "Try it", "--prompt-file", path], Token);

        Assert.Equal(0, code);
        Assert.Equal("Run the suite twice.", JsonDocument.Parse(Assert.Single(api.Calls).Body!).RootElement.GetProperty("prompt").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_prompt_file_exits_2_and_posts_nothing()
    {
        var api = Api();

        var code = await Verbs(api).Run(["send", "Try it", "--prompt-file", "/no/such/prompt.txt"], Token);

        Assert.Equal(2, code);
        Assert.Contains("--prompt-file", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Prompt_and_prompt_file_together_exit_2()
    {
        var api = Api();

        var code = await Verbs(api).Run(["send", "Try it", "--prompt", "a", "--prompt-file", "b"], Token);

        Assert.Equal(2, code);
        Assert.Empty(api.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--source")]
    [InlineData("--link")]
    [InlineData("--prompt-file")]
    public async Task A_flag_without_a_value_exits_2(string flag)
    {
        var api = Api();

        var code = await Verbs(api).Run(["send", "Try it", flag], Token);

        Assert.Equal(2, code);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_title_exits_2()
    {
        var api = Api();

        var code = await Verbs(api).Run(["send", "--source", "ci-bot"], Token);

        Assert.Equal(2, code);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_word_than_send_exits_2()
    {
        var code = await Verbs(Api()).Run(["receive"], Token);

        Assert.Equal(2, code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_post_exits_2()
    {
        var api = new RoutedApi().OnRaw("/sindri/handoff", HttpStatusCode.BadRequest,
            """{"type":"x","title":"SND-CLI-003","status":400,"detail":"the title is 0 characters (fix: 1 to 200)"}""");

        var code = await Verbs(api).Run(["send", "Try it"], Token);

        Assert.Equal(2, code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_control_plane_that_is_not_running_exits_1_and_posts_nothing()
    {
        var api = Api();
        var verbs = Verbs(api, (_, _) => Task.FromResult<Error?>(SindriErrors.NotRunning.With("not running (fix: sindri)")));

        var code = await verbs.Run(["send", "Try it"], Token);

        Assert.Equal(1, code);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_start_tells_the_runner_not_to_launch_the_control_plane()
    {
        bool? mayStart = null;
        var verbs = Verbs(Api(), (start, _) =>
        {
            mayStart = start;
            return Task.FromResult<Error?>(null);
        });

        _ = await verbs.Run(["send", "Try it", "--no-start"], Token);

        Assert.False(mayStart);
    }

    private static RoutedApi Api() =>
        new RoutedApi().OnPost("/sindri/handoff", HttpStatusCode.OK,
            new HandoffItemCreatedResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 4), A.FirstItemId, OfferKeyOf()));

    private static string OfferKeyOf() => "offer-7";

    private HandoffVerbs Verbs(RoutedApi api, Func<bool, CancellationToken, Task<Error?>>? ensure = null)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new HandoffVerbs(client, new Output(_out, _error, SliceHarness.Json), ensure ?? ((_, _) => Task.FromResult<Error?>(null)));
    }
}
