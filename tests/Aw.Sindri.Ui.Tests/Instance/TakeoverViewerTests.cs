using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>Take over on an agent Step sends the takeover command and says what the viewer did: opened in words, or the reason with the attach command to copy.</summary>
[Lane(TestLane.Unit)]
public sealed class TakeoverViewerTests
{
    private const string Instance = "WFI-0000000000000000000000000000000a";
    private const string Command = "tmux -L sindri-launch attach -t =snd-LCH-00000000000000000000000000000001";
    private const string AttachJson = """["tmux","-L","sindri-launch","attach","-t","=snd-LCH-00000000000000000000000000000001"]""";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ExpandedStateViewModel Ready()
    {
        var status = AgentRunFixtures.Status();
        var expanded = new ExpandedStateViewModel(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock)
        {
            Attacher = new AgentAttacher(_server, () => _api, "ITM-0000000000000000000000000000000e"),
            Commander = new InstanceCommander(_server, () => _api),
            InstanceId = Instance,
        };
        expanded.ShowRun(new AgentRunRead(Launch: null, AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));
        return expanded;
    }

    private void Accepted(string viewerJson, bool withAttach = true) =>
        _server.CommandAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"alreadyApplied":false{{(viewerJson.Length > 0 ? $",\"viewer\":{viewerJson}" : "")}}{{(withAttach ? $",\"attach\":{AttachJson}" : "")}}}""");

    private static async Task Until(Func<bool> done)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        while (!done())
        {
            await Task.Delay(10, limit.Token);
        }
    }

    [Fact]
    public async Task Take_over_sends_the_takeover_command_and_an_opened_viewer_shows_no_command()
    {
        Accepted("""{"kind":"warp","label":"Warp","outcome":"opened"}""");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.TakeOver);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Takeover), (sent.Instance, sent.Body.Kind));
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), expanded.AttachOutcomeLine);
        Assert.False(expanded.HasAttachCommand);
    }

    [Theory]
    [InlineData("""{"kind":"warp","label":"Warp","outcome":"skipped","reason":"background: viewer opens on takeover"}""", "The agent session is running. No terminal was opened: background: viewer opens on takeover")]
    [InlineData("""{"kind":"warp","label":"Warp","outcome":"failed","code":"SND-LAUNCH-001","detail":"warp: no display"}""", "The agent session is running. The viewer failed (SND-LAUNCH-001): warp: no display")]
    public async Task A_skipped_or_failed_viewer_shows_the_reason_and_the_attach_command_to_copy(string viewer, string line)
    {
        Accepted(viewer);
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.TakeOver);
        await Until(() => expanded.HasAttachCommand);
        await expanded.CopyAttachCommand(Token);

        Assert.Equal(line, expanded.AttachOutcomeLine);
        Assert.Equal(Command, expanded.AttachCommand);
        Assert.Equal(Command, _clipboard.Text);
    }

    [Fact]
    public async Task A_response_with_no_viewer_reads_the_launch_again()
    {
        Accepted(string.Empty, withAttach: false);
        _server.Launches[AgentRunFixtures.Launch] = RouteAnswer.Api("launches-id", "started-opened");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.TakeOver);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        Assert.Equal(1, _server.LaunchCalls);
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), expanded.AttachOutcomeLine);
    }

    [Fact]
    public async Task A_refused_takeover_shows_the_served_code()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "A session may not take over.");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.TakeOver);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        Assert.Equal(Labels.AttachRefusal("SND-AUTH-002"), expanded.AttachOutcomeLine);
        Assert.False(expanded.HasAttachCommand);
    }

    [Fact]
    public async Task The_instance_sheets_Take_over_command_sends_one_takeover_and_shows_the_outcome()
    {
        Accepted("""{"kind":"warp","label":"Warp","outcome":"opened"}""");
        var expanded = Ready();
        Assert.Contains(expanded.Commands, c => c.IsTakeOver);

        _ = expanded.TakeOverAsync();
        await Until(() => expanded.AttachOutcomeLine.Length > 0 && expanded.AttachOutcomeLine != Labels.AttachSending);

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Takeover), (sent.Instance, sent.Body.Kind));
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), expanded.AttachOutcomeLine);
    }

    [Fact]
    public async Task The_instance_sheets_refused_takeover_shows_the_served_code()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");
        var expanded = Ready();

        _ = expanded.TakeOverAsync();
        await Until(() => expanded.AttachOutcomeLine.Length > 0 && expanded.AttachOutcomeLine != Labels.AttachSending);

        Assert.Contains("SND-WF-041", expanded.AttachOutcomeLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_press_while_the_takeover_is_out_sends_nothing()
    {
        Accepted("""{"kind":"warp","label":"Warp","outcome":"opened"}""");
        _server.WorkflowCommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.TakeOver);
        expanded.Attach.Choose(AttachAction.TakeOver);
        await Until(() => _server.CommandCalls.Count > 0);
        _server.WorkflowCommandGate.SetResult();
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        _ = Assert.Single(_server.CommandCalls);
    }
}
