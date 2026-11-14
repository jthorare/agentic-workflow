namespace Aw.Sindri.Ui.Terminal;

/// <summary>The terminal WebSocket's messages: what the window sends, and how it reads what Serve sends (docs/sindri/agents.md).</summary>
internal static class TerminalSocketMessages
{
    /// <summary>The first message: the human token, never in the URL.</summary>
    internal static string Hello(string token, int cols, int rows) => string.Empty;

    /// <summary>A named key from Serve's list (Enter, Escape, C-c, F1...).</summary>
    internal static string Key(string name) => string.Empty;

    /// <summary>Literal text, at most 4 KiB.</summary>
    internal static string Text(string text) => string.Empty;

    /// <summary>A paste, at most 256 KiB.</summary>
    internal static string Paste(string text) => string.Empty;

    /// <summary>The size the view fits.</summary>
    internal static string Size(int cols, int rows) => string.Empty;

    /// <summary>The answer to a ping.</summary>
    internal static string Pong() => string.Empty;

    /// <summary>Ctrl-C to the pane: the named key C-c (no route is needed).</summary>
    internal static string Interrupt() => string.Empty;

    /// <summary>Turns a key the view saw into the message that sends it.</summary>
    /// <param name="name">The route name ("Enter", "Esc", "Ctrl+C", "A").</param>
    /// <param name="text">The characters the key typed, or null.</param>
    /// <returns>The JSON message, or null when the key has no tmux name and typed no text (it is dropped).</returns>
    internal static string? FromKey(string name, string? text) => null;

    /// <summary>Reads one server message.</summary>
    internal static TerminalServerMessage Parse(string json) => new(TerminalServerKind.Unknown, [], TerminalStatus.Unknown, 0);
}
