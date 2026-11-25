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

    protected override ValueTask<ITerminalPaneSource> Create() => Start("printf 'ready> '; cat");

    // The numbered rows are history once the screen (36 rows) has scrolled past them; 'cat' then waits for input like an agent does.
    protected override ValueTask<ITerminalPaneSource> CreateWithHistory(int lines) =>
        Start(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"seq 1 {lines + 40}; printf 'ready> '; cat"));

    // Each line typed into 'cat' comes back twice (the tty echo and cat's output), so 'lines' typed lines are at least 'lines' new rows.
    protected override ValueTask GrowHistory(ITerminalPaneSource source, int lines)
    {
        for (var i = 0; i < lines; i++)
        {
            Assert.NotNull(_tmux.Run("send-keys", "-t", Session, "-l", "--", "more"));
            Assert.NotNull(_tmux.Run("send-keys", "-t", Session, "Enter"));
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask EnterAlternateScreen(ITerminalPaneSource source)
    {
        // 'cat' echoes the escape bytes to the pane, so the pane's emulator switches screens.
        Assert.NotNull(_tmux.Run("send-keys", "-t", Session, "-l", "--", "\u001b[?1049h"));
        Assert.NotNull(_tmux.Run("send-keys", "-t", Session, "Enter"));
        return ValueTask.CompletedTask;
    }

    // 80 rows scroll into the normal screen's history, then the pane switches to the alternate screen (ESC [ ? 1049 h).
    protected override ValueTask<ITerminalPaneSource> CreateOnAlternateScreen() =>
        Start("seq 1 80; printf '\\033[?1049h'; printf 'alt> '; cat");

    private async ValueTask<ITerminalPaneSource> Start(string shell)
    {
        var host = new TmuxSessionHost(_tmux.Options);
        var session = new HostSession(Session, _tmux.Scratch, ["sh", "-c", shell], new Dictionary<string, string>(StringComparer.Ordinal))
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
