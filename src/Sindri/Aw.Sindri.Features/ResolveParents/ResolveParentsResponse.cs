using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Features.ResolveParents;

/// <summary>The freeze record of each parent, in the order the packet lists them.</summary>
/// <param name="Frozen">The records.</param>
public sealed record ResolveParentsResponse(IReadOnlyList<FrozenPacket> Frozen);
