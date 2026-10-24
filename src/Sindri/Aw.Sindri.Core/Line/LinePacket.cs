using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A station's packet and whether a freeze record holds its hash.</summary>
/// <param name="Kind">The packet's kind.</param>
/// <param name="Hash">The canonical hash, 'sha256:…'.</param>
/// <param name="Frozen">Whether aw-packets froze that hash (LN-PKT2's lookup).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinePacket(string Kind, string Hash, bool Frozen);
