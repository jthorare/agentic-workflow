using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.Tests.Takeover;

/// <summary>The takeover sender sends one takeover command per press and says what the viewer did: opened in words, or the reason with the attach command to copy.</summary>
[Lane(TestLane.Unit)]
public sealed class TakeoverSenderTests
{
    private const string Instance = "WFI-0000000000000000000000000000000a";
    private const string Command = "tmux -L sindri-launch attach -t =snd-LCH-00000000000000000000000000000001";
    private const string AttachJson = """["tmux","-L","sindri-launch","attach","-t","=snd-LCH-00000000000000000000000000000001"]""";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private void Accepted(string viewerJson, bool withAttach = true) =>
        _server.CommandAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"alreadyApplied":false{{(viewerJson.Length > 0 ? $",\"viewer\":{viewerJson}" : "")}}{{(withAttach ? $",\"attach\":{AttachJson}" : "")}}}""");

    private static async Task<List<TakeoverView>> PressOnce(TakeoverSender sender)
    {
        var shown = new List<TakeoverView>();
        await sender.Press(Instance, launchId: null, shown.Add);
        return shown;
    }

    [Fact]
    public async Task An_opened_viewer_shows_its_line_and_no_command()
    {
        Accepted("""{"kind":"warp","label":"Warp","outcome":"opened"}""");

        var shown = await PressOnce(new TakeoverSender(_server, () => _api));

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal(Instance, sent.Instance);
        Assert.Equal([Labels.AttachSending, Labels.LaunchOpenedIn("Warp")], shown.ConvertAll(v => v.Line));
        Assert.Equal(string.Empty, shown[^1].Command);
    }

    [Theory]
    [InlineData("""{"kind":"warp","label":"Warp","outcome":"skipped","reason":"background: viewer opens on takeover"}""", "The agent session is running. No terminal was opened: background: viewer opens on takeover")]
    [InlineData("""{"kind":"warp","label":"Warp","outcome":"failed","code":"SND-LAUNCH-001","detail":"warp: no display"}""", "The agent session is running. The viewer failed (SND-LAUNCH-001): warp: no display")]
    public async Task A_skipped_or_failed_viewer_shows_the_reason_and_the_attach_command_to_copy(string viewer, string line)
    {
        Accepted(viewer);

        var shown = await PressOnce(new TakeoverSender(_server, () => _api));

        Assert.Equal((line, Command), (shown[^1].Line, shown[^1].Command));
    }

    [Fact]
    public async Task A_refused_takeover_shows_the_served_code()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "A session may not take over.");

        var shown = await PressOnce(new TakeoverSender(_server, () => _api));

        Assert.Equal((Labels.TakeoverRefused("SND-AUTH-002", "A session may not take over."), string.Empty), (shown[^1].Line, shown[^1].Command));
    }

    [Fact]
    public async Task With_no_connection_nothing_is_sent_and_the_line_says_so()
    {
        var shown = await PressOnce(new TakeoverSender(_server, () => null));

        Assert.Empty(_server.CommandCalls);
        Assert.Equal(Labels.TakeoverNotSent, shown[^1].Line);
    }

    [Fact]
    public async Task A_second_press_while_the_takeover_is_out_sends_nothing()
    {
        Accepted("""{"kind":"warp","label":"Warp","outcome":"opened"}""");
        _server.WorkflowCommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TakeoverSender(_server, () => _api);

        var first = sender.Press(Instance, launchId: null, _ => { });
        await sender.Press(Instance, launchId: null, _ => { });
        _server.WorkflowCommandGate.SetResult();
        await first;

        _ = Assert.Single(_server.CommandCalls);
    }
}
