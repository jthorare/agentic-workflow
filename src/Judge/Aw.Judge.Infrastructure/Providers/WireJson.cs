using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>The AwJson options over <see cref="ProviderWireJsonContext"/>, and the reads every provider shares.</summary>
internal static class WireJson
{
    /// <summary>The options.</summary>
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(ProviderWireJsonContext.Default);

    /// <summary>Reads text as a wire record.</summary>
    /// <typeparam name="T">The record.</typeparam>
    /// <param name="text">The JSON text.</param>
    /// <returns>The record, or null when the text isn't one.</returns>
    public static T? Read<T>(string text)
        where T : class
    {
        var result = AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(text), Options);
        return result.IsError ? null : result.Value;
    }

    /// <summary>Parses text as any JSON value that can be read back; a repeated key or a lone-surrogate escape isn't one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="node">The value (null for JSON null).</param>
    /// <returns>Whether the text is JSON.</returns>
    public static bool TryParse(string text, out JsonNode? node) => InputJson.TryParse(text, out node);

    /// <summary>The input as compact JSON text, as the CLIs are given it.</summary>
    /// <param name="input">The input.</param>
    /// <returns>The text ('null' for none).</returns>
    public static string Text(JsonNode? input) => input?.ToJsonString() ?? "null";
}
