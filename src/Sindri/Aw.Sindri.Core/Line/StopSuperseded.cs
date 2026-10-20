using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>An item the design superseded.</summary>
/// <param name="Item">The item.</param>
/// <param name="Text">Its text.</param>
/// <param name="SupersededBy">The item that replaces it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopSuperseded(
    string Item,
    string Text,
    string SupersededBy);
