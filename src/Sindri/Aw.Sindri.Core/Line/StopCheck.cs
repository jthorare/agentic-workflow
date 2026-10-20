using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One rule's result for one item.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Status">pass, fail or not-run.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopCheck(
    string Rule,
    string Status);
