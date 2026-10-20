using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>An acceptance item.</summary>
/// <param name="Item">The item.</param>
/// <param name="Text">Its text.</param>
/// <param name="Citations">What it cites.</param>
/// <param name="Trust">owner or untrusted.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopItem(
    string Item,
    string Text,
    IReadOnlyList<string> Citations,
    string Trust);
