using System.Text.Json;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// A message's content, which every provider writes as a string or an array of blocks: the text blocks ('text',
/// 'input_text', 'output_text'), the tool calls' names ('tool_use') and whether it carries a tool result. Tool input
/// and output are never read.
/// </summary>
internal sealed record ContentBlocks(IReadOnlyList<string> Texts, IReadOnlyList<string> Tools, bool HasToolResult)
{
    private static readonly ContentBlocks _empty = new([], [], HasToolResult: false);

    /// <summary>The text blocks joined by newlines.</summary>
    public string Text => string.Join('\n', Texts);

    public static ContentBlocks Read(JsonElement? content)
    {
        if (content is not { } value)
        {
            return _empty;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return new ContentBlocks([value.GetString() ?? ""], [], HasToolResult: false);
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return _empty;
        }

        List<string> texts = [];
        List<string> tools = [];
        var toolResult = false;
        foreach (var block in value.EnumerateArray())
        {
            switch (Str(block, "type"))
            {
                case "text" or "input_text" or "output_text" when Str(block, "text") is { } text:
                    texts.Add(text);
                    break;
                case "tool_use" when Str(block, "name") is { Length: > 0 } name:
                    tools.Add(name);
                    break;
                case "tool_result":
                    toolResult = true;
                    break;
                default:
                    break;
            }
        }

        return new ContentBlocks(texts, tools, toolResult);
    }

    /// <summary>Whether a message's content has a shape the readers know: a string or an array of blocks.</summary>
    public static bool IsReadable(JsonElement? content) => content is { ValueKind: JsonValueKind.String or JsonValueKind.Array };

    /// <summary>A string member of a JSON object, or null.</summary>
    public static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
