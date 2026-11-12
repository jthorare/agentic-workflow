using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>#3183: on a fresh home the first code index build and the first library read are normal work, not a look.</summary>
[Lane(TestLane.Unit)]
public sealed class HealthFirstRunTests
{
    private const string Building = """{"repos":[{"repo":"repo","state":"building","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[]}""";
    private const string BuildingBehindMain = """{"repos":[{"repo":"a","state":"building","commit":null,"builtAt":null,"layers":[],"counts":null},{"repo":"b","state":"stale","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[]}""";

    private const string BuildingWithProgress = """{"repos":[{"repo":"repo","state":"building","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"repo","state":"building","embeddingsPercent":42,"waiting":null}]}""";
    private const string Missing = """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[]}""";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Idle(bool complete) =>
        $$"""{"sources":[{"source":"library","phase":"live","usable":true,"complete":{{(complete ? "true" : "false")}},"lastSync":null,"lastCode":null,"pull":"idle","records":0,"lastCommit":null}]}""";

    [Fact]
    public async Task A_repo_on_its_first_build_reads_building_and_keeps_its_line()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Building);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal("building", card.Word, StringComparer.Ordinal);
        Assert.Equal("repo: building", Assert.Single(card.Lines).Text);
    }

    [Fact]
    public async Task A_building_repo_shows_the_progress_the_server_serves()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, BuildingWithProgress);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal("building · embeddings 42%", card.Word, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_missing_index_stays_a_look_because_the_server_cannot_tell_it_from_a_queued_build()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Missing);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_building_repo_beside_a_stale_one_still_needs_a_look()
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, BuildingBehindMain);
        var card = new CodeIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_source_that_never_finished_a_read_reads_waiting_for_the_first_read()
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Idle(complete: false));
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal("waiting for the first read", card.Word, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_source_that_read_and_found_nothing_still_needs_a_look()
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Idle(complete: true));
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task First_run_work_alone_leaves_the_headline_healthy_and_the_card_plain()
    {
        var system = new SystemTestHarness();
        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });
        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Building);
        system.Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Idle(complete: false));

        await system.Vm.CodeIndex.Refresh(_api, Token);
        await system.Vm.ContextIndex.Refresh(_api, Token);

        Assert.Equal(Labels.AllRunning, system.Vm.Sentence);
        Assert.False(system.Vm.ToneWarn);
        Assert.False(system.Vm.ToneError);
    }

    [Fact]
    public async Task A_source_that_needs_a_look_still_names_the_context_index_beside_a_building_repo()
    {
        var system = new SystemTestHarness();
        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });
        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Building);
        system.Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Idle(complete: true));

        await system.Vm.CodeIndex.Refresh(_api, Token);
        await system.Vm.ContextIndex.Refresh(_api, Token);

        Assert.Equal("Something needs a look: context index.", system.Vm.Sentence);
        Assert.True(system.Vm.ToneWarn);
    }
}
