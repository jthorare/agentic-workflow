using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Embeddings;

/// <summary>The AwJson options over <see cref="OllamaJson"/>.</summary>
internal static class OllamaWire
{
    /// <summary>The options.</summary>
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(OllamaJson.Default);
}
