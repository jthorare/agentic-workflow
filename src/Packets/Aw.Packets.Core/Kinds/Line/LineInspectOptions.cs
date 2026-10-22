using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The optional inputs of a line packet inspection.</summary>
/// <param name="Sizes">The sizes file for a plan graph's PKT-SIZE-001; null leaves the rule unchecked.</param>
/// <param name="SealedSet">The hash the seal names for the phase's set; null leaves PKT-HOLD-003 unchecked.</param>
public sealed record LineInspectOptions(SizesFile? Sizes, PacketHash? SealedSet);
