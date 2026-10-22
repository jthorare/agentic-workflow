using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>What a line packet's checks read besides its files: the manifest as read, the base commit's files and the sizes file.</summary>
/// <param name="Read">The manifest as read.</param>
/// <param name="Tree">The base commit's files; null when the manifest is unreadable.</param>
/// <param name="Sizes">The sizes file; null leaves PKT-SIZE-001 unchecked.</param>
/// <param name="SealedSet">The set hash the seal names; null leaves PKT-HOLD-003 unchecked.</param>
internal sealed record LineInputs(ManifestRead Read, BaseTree? Tree, SizesFile? Sizes, PacketHash? SealedSet = null);
