using System.Text.Json.Serialization;

namespace Aw.Embeddings;

/// <summary>Ollama's wire formats; only ever used through <see cref="OllamaWire.Options"/> (the AwJson profile).</summary>
[JsonSerializable(typeof(EmbedRequest))]
[JsonSerializable(typeof(EmbedResponse))]
[JsonSerializable(typeof(TagsResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class OllamaJson : JsonSerializerContext;
