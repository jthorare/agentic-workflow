using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.TerminalSocket;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>The terminal route's pane lookup (agents spec §15.2, §15.8): it runs before the WebSocket upgrade, so a session without a pane is refused as HTTP.</summary>
[Lane(TestLane.Unit)]
public sealed class FindTerminalPaneServiceTests
{
    private static readonly SessionRef _live = new(AgentId.From("claude-code"), SessionId.From("live"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindTerminalPane_answers_the_pane_the_locator_finds()
    {
        var pane = new TerminalTarget("sindri", SocketIsPath: false, "sindri-act1", "%3", Foreign: false);
        var service = new FindTerminalPaneService(new Locator(pane));

        var found = await service.Handle(new FindTerminalPaneRequest(_live), Token);

        Assert.Same(pane, found.Value.Pane);
    }

    [Fact]
    public async Task FindTerminalPane_passes_on_no_pane_and_pane_gone()
    {
        var noPane = await new FindTerminalPaneService(new Locator(SindriErrors.AgentNoPane.With("warp"))).Handle(new FindTerminalPaneRequest(_live), Token);
        var gone = await new FindTerminalPaneService(new Locator(SindriErrors.AgentPaneGone.With("gone"))).Handle(new FindTerminalPaneRequest(_live), Token);

        Assert.Equal(SindriErrors.AgentNoPane.Code, noPane.Error.Code);
        Assert.Equal(SindriErrors.AgentPaneGone.Code, gone.Error.Code);
    }

    private sealed class Locator(Result<TerminalTarget> answer) : ITerminalPaneLocator
    {
        public ValueTask<Result<TerminalTarget>> Find(SessionRef session, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
