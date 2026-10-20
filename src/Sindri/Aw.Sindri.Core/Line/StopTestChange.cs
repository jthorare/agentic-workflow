using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>An existing test file a phase may change.</summary>
/// <param name="Path">The file.</param>
/// <param name="Reason">Why.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopTestChange(
    string Path,
    string Reason);
