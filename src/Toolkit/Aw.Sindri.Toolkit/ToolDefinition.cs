using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Toolkit;

/// <summary>One tool a toolkit serves: its name, description and the JSON schemas of its request and response.</summary>
public sealed class ToolDefinition
{
    internal ToolDefinition(
        string name,
        string description,
        JsonObject inputSchema,
        JsonObject outputSchema,
        Func<JsonElement, ToolkitContext, CancellationToken, Task<JsonElement>> invoke)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        OutputSchema = outputSchema;
        Invoke = invoke;
    }

    /// <summary>The tool name.</summary>
    public string Name { get; }

    /// <summary>What the tool does, for the agent that picks it.</summary>
    public string Description { get; }

    /// <summary>The JSON schema of the request record: a required property per non-nullable member.</summary>
    public JsonObject InputSchema { get; }

    /// <summary>The JSON schema of the response record.</summary>
    public JsonObject OutputSchema { get; }

    /// <summary>Runs the tool: the request as a JSON object in, the response as JSON out.</summary>
    internal Func<JsonElement, ToolkitContext, CancellationToken, Task<JsonElement>> Invoke { get; }
}
