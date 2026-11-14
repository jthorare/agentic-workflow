using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>The server messages of the terminal WebSocket (docs/sindri/agents.md, the message table): 'ready' and 'frame', as UTF-8 JSON text.</summary>
internal static class TerminalSocketText
{
    internal static string Ready(TerminalHost host)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("t", "ready");
            json.WriteNumber("v", 1);
            json.WriteStartObject("host");
            json.WriteString("socket", host.Socket);
            json.WriteString("session", host.Session);
            json.WriteString("pane", host.Pane);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string Frame(TerminalFrame frame)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("t", "frame");
            json.WriteNumber("seq", frame.Seq);
            json.WriteStartArray("rows");
            foreach (var row in frame.Rows)
            {
                json.WriteStringValue(row);
            }

            json.WriteEndArray();
            json.WriteStartObject("cursor");
            json.WriteNumber("x", frame.CursorX);
            json.WriteNumber("y", frame.CursorY);
            json.WriteEndObject();
            json.WriteNumber("w", frame.Width);
            json.WriteNumber("h", frame.Height);
            json.WriteNumber("alt", frame.Alt ? 1 : 0);
            json.WriteNumber("inMode", frame.InMode ? 1 : 0);
            json.WriteNumber("dead", frame.Dead ? 1 : 0);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
