namespace Aw.Sindri.Core.Packets;

/// <summary>One freeze record.</summary>
/// <param name="Hash">The packet's canonical hash.</param>
/// <param name="Packet">The packet's id under its repository.</param>
/// <param name="Kind">The packet's kind ('design', 'plan' …; 'build' when the record names none).</param>
/// <param name="By">Who froze it.</param>
public sealed record FrozenPacket(string Hash, string Packet, string Kind, PacketFreezer By);
