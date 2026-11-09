namespace Aw.Sindri.Core.Events;

/// <summary>What an approval pins (spec §4.1, W11): the descriptor hash of each block the recipe names and the hash of each child, both sorted by id.</summary>
/// <param name="Blocks">The block pins.</param>
/// <param name="Children">The child pins.</param>
/// <remarks>Two pin sets are equal when they hold the same pins in the same order, so an approval read back from a store equals the one written.</remarks>
public sealed record RecipePins(IReadOnlyList<BlockPin> Blocks, IReadOnlyList<ChildPin> Children)
{
    /// <summary>No pins.</summary>
    public static RecipePins None { get; } = new([], []);

    /// <summary>Whether <paramref name="other"/> holds the same block and child pins, in the same order.</summary>
    /// <param name="other">The other pins.</param>
    /// <returns>True when both lists are equal item by item.</returns>
    public bool Equals(RecipePins? other) =>
        other is not null && (ReferenceEquals(this, other) || (Blocks.SequenceEqual(other.Blocks) && Children.SequenceEqual(other.Children)));

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var block in Blocks)
        {
            hash.Add(block);
        }

        foreach (var child in Children)
        {
            hash.Add(child);
        }

        return hash.ToHashCode();
    }
}
