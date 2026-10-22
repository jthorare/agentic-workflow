namespace Aw.Packets.App.Features.ValidateLinePacket;

/// <summary>The ValidateLinePacket request.</summary>
/// <param name="Kind">A line kind's name: design, plan, phase, proof, review or handoff.</param>
/// <param name="PacketDirectory">The packet directory (holds packet.json).</param>
/// <param name="RepoDirectory">A directory inside the repository the packet is about; the packet directory when null.</param>
/// <param name="SealFile">The seal file a phase packet's PKT-HOLD-003 compares its holdout set hash with (LN-PKT4); null leaves the rule unchecked.</param>
/// <param name="SizesFile">The sizes file a plan graph's PKT-SIZE-001 checks every task against (LN-PKT3); null leaves the rule unchecked.</param>
internal sealed record ValidateLinePacketRequest(string Kind, string PacketDirectory, string? RepoDirectory = null, string? SizesFile = null, string? SealFile = null);
