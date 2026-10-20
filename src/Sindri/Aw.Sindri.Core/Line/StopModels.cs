using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>The models behind the design.</summary>
/// <param name="Writer">The writer run, or null when it is not recorded.</param>
/// <param name="Lens">The lens run, or null when it is not recorded.</param>
/// <param name="Warnings">The DSN-LENS-002 warnings, shown first.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopModels(
    StopModel? Writer,
    StopModel? Lens,
    IReadOnlyList<string> Warnings);
