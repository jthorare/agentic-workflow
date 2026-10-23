using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A tool's arguments as the MCP client sends them: the record written through the T3 context, split into its top-level properties.</summary>
internal static class T3Arguments
{
    /// <summary>The arguments of <paramref name="value"/>.</summary>
    /// <typeparam name="T">The arguments record.</typeparam>
    /// <param name="value">The arguments.</param>
    /// <param name="json">The T3 context's options.</param>
    /// <returns>One element per property.</returns>
    public static Dictionary<string, JsonElement> Of<T>(T value, JsonSerializerOptions json)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(value, json));
        return document.RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.Clone(), StringComparer.Ordinal);
    }
}
