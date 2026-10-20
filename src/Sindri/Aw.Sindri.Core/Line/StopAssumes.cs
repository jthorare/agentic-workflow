using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A section that assumes an option.</summary>
/// <param name="Section">The section.</param>
/// <param name="Decision">The decision.</param>
/// <param name="Option">The option it assumes.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopAssumes(
    string Section,
    string Decision,
    string Option);
