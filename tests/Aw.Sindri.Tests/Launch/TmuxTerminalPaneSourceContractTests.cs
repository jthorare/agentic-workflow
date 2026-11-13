using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The real tmux pane source on a private tmux server (agents spec §15.7): the same contract as the fake, so the two cannot drift. Heavy: it starts tmux.</summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxTerminalPaneSourceContractTests : TerminalPaneSourceContract, IDisposable
{
    private const string Session = "sindri-abcd1234";
    private readonly PrivateTmux _tmux = new();

    public void Dispose() => _tmux.Dispose();

    protected override async ValueTask<ITerminalPaneSource> Create()
    {
        var host = new TmuxSessionHost(_tmux.Options);
        var session = new HostSession(Session, _tmux.Scratch, ["sh", "-c", "printf 'ready> '; cat"], new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Size = new HostSize(120, 36),
        };
        Assert.True((await host.Start(session, TimeSpan.FromSeconds(4), CancellationToken.None)).IsSuccess);
        return new TmuxTerminalPaneSource(_tmux.Options, new OneTarget(new TerminalTarget(_tmux.Options.Socket, SocketIsPath: false, Session, Session, Foreign: false)));
    }

    protected override ValueTask<ITerminalPaneSource> CreateRefusing(string code) =>
        ValueTask.FromResult<ITerminalPaneSource>(new TmuxTerminalPaneSource(
            _tmux.Options,
            new OneTarget(code == SindriErrors.AgentNoPane.Code ? SindriErrors.AgentNoPane.With("warp") : SindriErrors.AgentPaneGone.With("gone"))));

    private sealed class OneTarget(Result<TerminalTarget> answer) : ITerminalPaneLocator
    {
        public ValueTask<Result<TerminalTarget>> Find(SessionRef session, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
