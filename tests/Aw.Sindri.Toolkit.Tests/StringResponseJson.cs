using System.Text.Json.Serialization;

namespace Aw.Sindri.Toolkit.Tests;

/// <summary>A context whose only response is a bare string, to prove the SDK refuses a non-object output schema.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(DiffRequest))]
[JsonSerializable(typeof(string))]
internal sealed partial class StringResponseJson : JsonSerializerContext;
