namespace Aw.Sindri.Core.Line;

/// <summary>What a packet's packet.json says about it besides its links.</summary>
/// <param name="Packet">The packet's id under its repository, for example 'design-expiry'.</param>
/// <param name="Repo">'owner/name'.</param>
/// <param name="BaseSha">The commit the packet was written against.</param>
public sealed record PacketIdentity(string Packet, string Repo, string BaseSha);
