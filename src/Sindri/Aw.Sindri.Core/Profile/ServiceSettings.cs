using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A background service's settings.</summary>
/// <param name="Every">Its cadence: a whole number and a unit, s, m or h (for example 30s), from 5s to 24h.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceSettings(string Every);
