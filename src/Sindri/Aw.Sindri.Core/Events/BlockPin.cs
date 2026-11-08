using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// One pinned block (W11): the descriptor hash of a block the recipe names, as the approval saw it, and its interface hash (the
/// descriptor's hash with its version left out). Two pins are equal when block and descriptor hash are: the interface only
/// says whether a changed hash is a version bump alone (Compatible).
/// </summary>
/// <param name="Block">The block id.</param>
/// <param name="Hash">Its descriptor hash at approval.</param>
/// <param name="Interface">Its interface hash at approval; null on an approval from before it was pinned.</param>
public sealed record BlockPin(BlockId Block, DescriptorHash Hash, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DescriptorHash? Interface = null)
{
    /// <summary>Whether <paramref name="other"/> pins the same block at the same descriptor hash; the interface hash doesn't count.</summary>
    /// <param name="other">The other pin.</param>
    /// <returns>True when they are equal.</returns>
    public bool Equals(BlockPin? other) => other is not null && Block == other.Block && Hash == other.Hash;

    /// <summary>The hash of the block and its descriptor hash.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(Block, Hash);
}
