using System.Text.Json.Serialization;

namespace Aw.Sindri.Toolkit.Tests;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(DiffRequest))]
[JsonSerializable(typeof(DiffResponse))]
internal sealed partial class SampleJson : JsonSerializerContext;
