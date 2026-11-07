using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.linear' as this build reads it: the team keys that scope every read and an identifier alias, and (for the native adapter) the API url the secret pointer to the key and, for writes, the pointer to the write key.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LinearSection(IReadOnlyList<string>? Teams = null, string? Url = null, string? Token = null, string? WriteToken = null);
