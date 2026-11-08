using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>What a ready Home row action does when the shell's Decide hands it on: each sends once, a refusal is said as served, and an action with no send says so (never dropped silently).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouDispatcherTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private NeedsYouDispatcher Dispatcher() =>
        new(_server, () => _api);

    private static NeedsYouAction Service(string id, string label) => new(id, label, ActionReadiness.Ready, "sindri services " + id, RejectLike: false);

    [Fact]
    public async Task Pause_service_pauses_the_rows_service_by_name()
    {
        var result = await Dispatcher().Run(NeedsYouRows.Failure(), Service("PauseService", "Pause service"));

        Assert.Equal(["index"], _server.PauseServiceCalls);
        Assert.Equal(DispatchOutcome.Sent, result.Outcome);
    }

    [Fact]
    public async Task Resume_service_and_run_service_send_their_own_route()
    {
        var dispatcher = Dispatcher();

        _ = await dispatcher.Run(NeedsYouRows.Failure(), Service("ResumeService", "Resume service"));
        _ = await dispatcher.Run(NeedsYouRows.Failure(), Service("RunService", "Run again"));

        Assert.Equal(["index"], _server.ResumeServiceCalls);
        Assert.Equal(["index"], _server.RunServiceCalls);
    }

    [Fact]
    public async Task An_action_the_app_cannot_send_says_so_and_sends_nothing()
    {
        var other = new NeedsYouAction("SomethingNew", "Something new", ActionReadiness.Ready, "sindri something new", RejectLike: false);

        var result = await Dispatcher().Run(NeedsYouRows.Failure(), other);

        Assert.Equal((DispatchOutcome.Unhandled, Labels.RowNotAvailable("sindri something new")), (result.Outcome, result.Message));
        Assert.Empty(_server.PauseServiceCalls);
    }

}
