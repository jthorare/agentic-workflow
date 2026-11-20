using System.Text;
using System.Text.Json;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>The terminal WebSocket's messages: what the window sends, and how it reads what Serve sends (docs/sindri/agents.md).</summary>
internal static class TerminalSocketMessages
{
    private static readonly Dictionary<string, string> _named = new(StringComparer.Ordinal)
    {
        ["Enter"] = "Enter", ["Return"] = "Enter", ["Esc"] = "Escape", ["Escape"] = "Escape", ["Tab"] = "Tab", ["Back"] = "BSpace", ["Delete"] = "DC",
        ["Up"] = "Up", ["Down"] = "Down", ["Left"] = "Left", ["Right"] = "Right", ["Home"] = "Home", ["End"] = "End",
        ["PageUp"] = "PageUp", ["Prior"] = "PageUp", ["PageDown"] = "PageDown", ["Next"] = "PageDown",
    };

    /// <summary>The most UTF-8 bytes one paste may hold: the cap Serve closes the socket over (1009).</summary>
    internal const int MaxPasteBytes = 256 * 1024;

    /// <summary>The most bytes one encoded message may hold: Serve closes the socket (1009) over a larger one, before it decodes the text.</summary>
    internal const int MaxMessageBytes = 512 * 1024;

    /// <summary>The first message: the human token, never in the URL.</summary>
    internal static string Hello(string token, int cols, int rows) => Write(w =>
    {
        w.WriteString("t", "hello");
        w.WriteNumber("v", 1);
        w.WriteString("token", token);
        w.WriteNumber("cols", cols);
        w.WriteNumber("rows", rows);
    });

    /// <summary>A named key from Serve's list (Enter, Escape, C-c, F1...).</summary>
    internal static string Key(string name) => Write(w =>
    {
        w.WriteString("t", "key");
        w.WriteString("key", name);
    });

    /// <summary>Literal text, at most 4 KiB.</summary>
    internal static string Text(string text) => Write(w =>
    {
        w.WriteString("t", "key");
        w.WriteString("text", text);
    });

    /// <summary>A paste, at most 256 KiB.</summary>
    internal static string Paste(string text) => Write(w =>
    {
        w.WriteString("t", "paste");
        w.WriteString("text", text);
    });

    /// <summary>The size the view fits.</summary>
    internal static string Size(int cols, int rows) => Write(w =>
    {
        w.WriteString("t", "size");
        w.WriteNumber("cols", cols);
        w.WriteNumber("rows", rows);
    });

    /// <summary>The answer to a ping.</summary>
    internal static string Pong() => Write(w => w.WriteString("t", "pong"));

    /// <summary>Ctrl-C to the pane: the named key C-c (no route is needed).</summary>
    internal static string Interrupt() => Key("C-c");

    /// <summary>Turns a key the view saw into the message that sends it.</summary>
    /// <param name="name">The route name ("Enter", "Esc", "Ctrl+C", "A").</param>
    /// <param name="text">The characters the key typed, or null.</param>
    /// <returns>The JSON message, or null when the key has no tmux name and typed no text (it is dropped).</returns>
    internal static string? FromKey(string name, string? text)
    {
        var parts = name.Split('+');
        var key = parts[^1];
        var mods = parts[..^1];
        var (ctrl, alt, shift, meta) = (mods.Contains("Ctrl", StringComparer.Ordinal), mods.Contains("Alt", StringComparer.Ordinal), mods.Contains("Shift", StringComparer.Ordinal), mods.Contains("Cmd", StringComparer.Ordinal));
        if (meta)
        {
            return null;
        }

        if (key.Length == 1 && char.IsAsciiLetter(key[0]) && (ctrl || alt))
        {
            return Key((ctrl ? "C-" : "M-") + char.ToLowerInvariant(key[0]));
        }

        if (shift && string.Equals(key, "Tab", StringComparison.Ordinal))
        {
            return Key("BTab");
        }

        if (_named.TryGetValue(key, out var named))
        {
            return Key(named);
        }

        if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var f) && f is >= 1 and <= 12)
        {
            return Key(key);
        }

        return text is { Length: > 0 } && !ctrl && !alt ? Text(text) : null;
    }

    /// <summary>Reads one server message.</summary>
    internal static TerminalServerMessage Parse(string json)
    {
        var unknown = new TerminalServerMessage(TerminalServerKind.Unknown, [], TerminalStatus.Unknown, 0);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (root.TryGetProperty("t", out var t) ? t.GetString() : null) switch
            {
                "ready" => unknown with { Kind = TerminalServerKind.Ready },
                "ping" => unknown with { Kind = TerminalServerKind.Ping },
                "frame" => Frame(root),
                _ => unknown,
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            return unknown;
        }
    }

    private static TerminalServerMessage Frame(JsonElement root)
    {
        var rows = root.GetProperty("rows").EnumerateArray().Select(r => r.GetString() ?? string.Empty).ToList();
        var cursor = root.GetProperty("cursor");
        var status = new TerminalStatus(
            root.GetProperty("w").GetInt32(), root.GetProperty("h").GetInt32(), cursor.GetProperty("x").GetInt32(), cursor.GetProperty("y").GetInt32(),
            root.GetProperty("inMode").GetInt32() != 0, root.GetProperty("alt").GetInt32() != 0, root.GetProperty("dead").GetInt32() != 0, null);
        return new TerminalServerMessage(TerminalServerKind.Frame, rows, status, root.GetProperty("seq").GetInt32());
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
