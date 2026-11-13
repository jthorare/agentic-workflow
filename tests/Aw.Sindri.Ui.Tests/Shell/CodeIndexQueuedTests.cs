using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>#3369: a repo whose code index the server serves as 'queued' (no index yet, a build starts within about a minute) is normal work, not a look; 'waiting' (retrying in N min) stays one.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexQueuedTests
{
    private const string Queued = """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"repo","state":"queued","embeddingsPercent":null,"waiting":null}]}""";
    private const string Waiting = """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"repo","state":"waiting","embeddingsPercent":null,"waiting":"retrying in 5 min"}]}""";
    private const string QueuedBehindMain = """{"repos":[{"repo":"a","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null},{"repo":"b","state":"stale","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"a","state":"queued","embeddingsPercent":null,"waiting":null}]}""";

    private const string QueuedTooLong = """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"repo","state":"queued","embeddingsPercent":null,"waiting":null,"needsLook":true}]}""";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_queued_repo_reads_build_queued_and_keeps_its_line()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Queued);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal("build queued", card.Word, StringComparer.Ordinal);
        Assert.NotEqual(Labels.CodeIndexNeedsLook, card.Word, StringComparer.Ordinal);
        Assert.Equal("repo: build queued", Assert.Single(card.Lines).Text);
    }

    [Fact]
    public async Task A_queued_repo_leaves_the_headline_healthy()
    {
        var system = new SystemTestHarness();
        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });
        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Queued);

        await system.Vm.CodeIndex.Refresh(_api, Token);

        Assert.Equal(Labels.AllRunning, system.Vm.Sentence);
        Assert.False(system.Vm.ToneWarn);
    }

    [Fact]
    public async Task A_queued_repo_the_server_flags_needsLook_reads_needs_a_look()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, QueuedTooLong);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_queued_repo_the_server_flags_needsLook_names_the_code_index_in_the_headline()
    {
        var system = new SystemTestHarness();
        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });
        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, QueuedTooLong);

        await system.Vm.CodeIndex.Refresh(_api, Token);

        Assert.Contains(Labels.CodeIndexLookName, system.Vm.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_queued_repo_beside_a_stale_one_still_needs_a_look()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, QueuedBehindMain);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_waiting_repo_stays_a_look_and_shows_the_servers_retry_words()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Waiting);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
        Assert.Equal("repo: retrying in 5 min", Assert.Single(card.Lines).Text);
    }
}
