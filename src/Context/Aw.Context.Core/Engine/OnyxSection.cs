using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.onyx' as written: Onyx's base URL, the secret pointer to its key and the pinned release tag; 'network' is 'loopback' (the default) or 'cluster' (pod mode only).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record OnyxSection(string? Url = null, string? Token = null, string? Version = null, bool? Managed = null, string? Network = null);
