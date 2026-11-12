using Avalonia.Controls;
using Avalonia.Threading;
using Devolutions.Terminal;
using Devolutions.Terminal.Settings;

namespace Aw.Spike.T3335;

/// <summary>Candidate A: Devolutions TermControl running `tmux -L sindri-spike attach -t session` on a pty (dt-pty-host).</summary>
internal sealed class PaneA : IPane
{
    private readonly TermControl _term = new();

    public string Name => "a";
    public Control View => _term;
    public (int Cols, int Rows) Grid => (_term.Engine.Columns, _term.Engine.Rows);
    public event Action? Updated;

    public async Task StartAsync(string session)
    {
        _term.Engine.Invalidated += (_, _) => Dispatcher.UIThread.Post(() => Updated?.Invoke(), DispatcherPriority.Normal);
        var profile = new ProfileSettings
        {
            Commandline = $"tmux -L {SpikeTmux.Socket} attach -t {session}",
            StartingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        profile.Environment["TMUX"] = null;
        profile.Environment["TMUX_PANE"] = null;
        await _term.StartAsync(profile, 120, 30);
        _term.Focus();
    }

    public Task CloseAsync() => _term.CloseAsync();

    public void SendText(string text) => _term.WriteInput(text);

    public string VisibleText() => _term.Engine.Buffer.GetVisibleText();

    public void ScrollToTop() => _term.ScrollToTop();

    public void ScrollToBottom() => _term.ScrollToBottom();
}
