using Avalonia.Controls;

namespace Aw.Spike.T3335;

/// <summary>One way to show a tmux session: A = VT emulator over a pty attach, C = capture-pane poll.</summary>
internal interface IPane
{
    string Name { get; }
    Control View { get; }
    (int Cols, int Rows) Grid { get; }
    event Action? Updated;
    Task StartAsync(string session);
    Task CloseAsync();
    void SendText(string text);
    string VisibleText();
    void ScrollToTop();
    void ScrollToBottom();
}
