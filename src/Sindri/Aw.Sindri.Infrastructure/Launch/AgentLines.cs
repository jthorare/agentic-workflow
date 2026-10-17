using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Finds the line of each key under 'agents' in launcher.json text, so doctor can show a dropped entry with its line.</summary>
internal static class AgentLines
{
    /// <summary>The 1-based line of each entry key directly under the top-level 'agents' object.</summary>
    /// <param name="json">The file's bytes; they parsed already.</param>
    /// <returns>Entry id to line.</returns>
    internal static Dictionary<string, int> Of(byte[] json)
    {
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var reader = new Utf8JsonReader(json);
        var inAgents = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
            {
                inAgents = reader.ValueTextEquals("agents"u8);
            }
            else if (inAgents && reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 2)
            {
                lines[reader.GetString()!] = 1 + json.AsSpan(0, (int)reader.TokenStartIndex).Count((byte)'\n'); // why: a property name always has a string
            }
        }

        return lines;
    }
}
