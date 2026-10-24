using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One station of the line.</summary>
/// <param name="Station">'scoping', 'design' or 'plan'.</param>
/// <param name="Instance">The station's live child instance address; null once it ended or before it started.</param>
/// <param name="Packet">The packet the station produced; null before it has one.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineStation(string Station, string? Instance, LinePacket? Packet);
