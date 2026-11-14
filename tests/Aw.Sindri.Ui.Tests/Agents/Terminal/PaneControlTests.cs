using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The real session control's shape: Stop is POST .../{provider}/{id}/stop and says why it failed; Interrupt goes through the socket.</summary>
[Lane(TestLane.Unit)]
public sealed class PaneControlTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stop_splits_the_ref_into_provider_and_id_for_the_stop_route()
    {
        var stopper = new FakeSessionStopper();
        var control = new PaneControl(stopper, () => _api, _ => Task.FromResult(true));

        var stopped = await control.Stop("claude-code/abc", Token);

        Assert.True(stopped);
        Assert.Equal(["claude-code/abc"], stopper.Stopped);
        Assert.Equal(string.Empty, control.Failure);
    }

    [Fact]
    public async Task A_404_names_the_missing_route_and_the_line_shows_it()
    {
        var control = new PaneControl(new FakeSessionStopper { Refuse = true }, () => _api, _ => Task.FromResult(true));
        var f = new TerminalFixture();
        var view = new TerminalViewViewModel(f.Clock, isMac: false, control);
        view.Open(TerminalFixture.Started(), TerminalOpenIntent.Look);
        view.SocketOpening();
        _ = view.Ready();

        _ = await view.PressStop(Token);
        _ = await view.PressStop(Token);

        Assert.Equal("Couldn't stop it (404: this Sindri has no stop route yet).", view.ControlLine);
    }

    [Fact]
    public async Task Stop_with_no_known_api_address_fails_without_calling_the_stopper()
    {
        var stopper = new FakeSessionStopper();
        var control = new PaneControl(stopper, () => null, _ => Task.FromResult(true));

        Assert.False(await control.Stop("claude-code/abc", Token));
        Assert.Empty(stopper.Stopped);
    }

    [Fact]
    public async Task Interrupt_calls_the_socket_not_a_route()
    {
        var sent = 0;
        var control = new PaneControl(new FakeSessionStopper(), () => _api, _ => Task.FromResult(++sent > 0));

        Assert.True(await control.Interrupt("claude-code/abc", Token));
        Assert.Equal(1, sent);
    }
}
