using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>Why freeze refused after an approve.</summary>
/// <param name="Rule">The rule that refused.</param>
/// <param name="Message">What it said.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopFreezeRefused(
    string Rule,
    string Message);
