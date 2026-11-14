using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the terminal view's header words, footers and cards (agents spec §15.3, §15.5). Never a served sentence.</summary>
internal static partial class Labels
{
    internal const string TerminalConnecting = "Connecting";

    internal const string TerminalReconnecting = "Reconnecting";

    internal const string TerminalCantReach = "Can't reach Sindri";

    /// <summary>Close code 1008: Serve refused the window's token; retrying can't help.</summary>
    internal const string TerminalTokenRefused = "Sindri didn't accept this window's token.";

    internal const string TerminalTakenOver = "This pane is open in another Sindri window";

    internal const string TerminalStarting = "Starting";

    internal const string TerminalAttached = "Attached";

    internal const string TerminalCantAttach = "Can't attach";

    internal const string TerminalGone = "Gone";

    internal const string TerminalEnded = "Ended";

    /// <summary>The card's reason when tmux or the session is gone (SND-AGENT-016).</summary>
    internal const string TerminalSessionGone = "tmux session gone";

    /// <summary>The card's reason when the server says the session has no pane (SND-AGENT-015) and the row gave none.</summary>
    internal const string TerminalNoPane = "Sindri can't show or type into this terminal.";

    /// <summary>Shown beside "Can't reach Sindri".</summary>
    internal const string TerminalEscCloses = "Esc closes the view";

    internal const string TerminalRetryNow = "Retry now";

    internal const string TerminalTakeItBack = "Take it back";

    internal const string TerminalStop = "Stop…";

    internal const string TerminalStopAgain = "Stop? Press again";

    internal const string TerminalInterrupt = "Interrupt";

    internal const string TerminalStopFailed = "Couldn't stop it.";

    internal const string TerminalInterruptFailed = "Couldn't send Ctrl-C.";

    /// <summary>The line under the header when a paste is over the socket's cap (#3729).</summary>
    internal const string TerminalPasteTooBig = "Paste too big (max 256 KiB)";

    internal const string TerminalJump = "Jump to it";

    internal const string TerminalCopyResume = "Copy resume command";

    internal const string TerminalRemoveWorktree = "Remove worktree";

    internal const string TerminalDiscardSession = "Discard session";

    /// <summary>The footer while Reconnecting: no button, so F6 does nothing.</summary>
    internal const string TerminalFooterReconnecting = "Reconnecting · Esc close · n new";

    /// <summary>The list-focus footer of a card state.</summary>
    /// <param name="button">The first button's label.</param>
    internal static string TerminalFooterListCard(string button) => "F6 to " + button + " · Esc close · n new";

    /// <summary>The card-focus footer.</summary>
    /// <param name="button">The focused button's label.</param>
    internal static string TerminalFooterCard(string button) => "Enter to " + button + " · F6 back to the list";

    /// <summary>The footer when the list has focus but there is nothing to type into (can't attach, ended, gone).</summary>
    internal const string TerminalFooterListOnly = "Esc close · n new";

    /// <summary>The footer while the pane has focus.</summary>
    /// <param name="name">The target's name.</param>
    /// <param name="isMac">True on macOS, where the second chord is Cmd+Shift+L.</param>
    internal static string TerminalFooterPane(string name, bool isMac) =>
        "Typing goes to " + name + " · F6 or " + TerminalSecondChord(isMac) + " back to the list · Shift+PgUp scroll back";

    /// <summary>The footer while the list has focus and the pane is (or will be) live.</summary>
    /// <param name="name">The target's name.</param>
    /// <param name="isMac">True on macOS.</param>
    internal static string TerminalFooterList(string name, bool isMac) =>
        isMac
            ? "Enter again, F6 or " + TerminalSecondChord(isMac) + " to type in " + name + " · Esc close · n new"
            : "Enter again, or F6, to type in " + name + " · Esc close · n new";

    /// <summary>The key chord a terminal never receives.</summary>
    /// <param name="isMac">True on macOS.</param>
    internal static string TerminalSecondChord(bool isMac) => isMac ? "Cmd+Shift+L" : "Ctrl+Shift+L";

    /// <summary>"tmux default" plus the other clients.</summary>
    /// <param name="socketKind">sindri, default or team.</param>
    /// <param name="others">Other clients attached to the session.</param>
    internal static string TerminalHost(string socketKind, int others) =>
        "tmux " + socketKind + (others switch
        {
            <= 0 => string.Empty,
            1 => " · 1 other client",
            _ => " · " + others.ToString(CultureInfo.InvariantCulture) + " other clients",
        });

    /// <summary>"Ended 14:02".</summary>
    /// <param name="at">The local time the session ended.</param>
    internal static string TerminalEndedAt(DateTimeOffset at) => TerminalEnded + " " + at.ToString("HH:mm", CultureInfo.InvariantCulture);
}
