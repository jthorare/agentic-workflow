using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>One entry of a ps row's Publishers.</summary>
internal sealed record ComposePsPublisher(
    [property: JsonPropertyName("URL")] string? Url,
    [property: JsonPropertyName("TargetPort")] int? TargetPort,
    [property: JsonPropertyName("PublishedPort")] int? PublishedPort);
