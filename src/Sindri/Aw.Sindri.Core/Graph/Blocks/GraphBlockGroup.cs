namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One picker group and its blocks.</summary>
public sealed class GraphBlockGroup
{
    /// <summary>Makes a group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="blocks">Its blocks.</param>
    public GraphBlockGroup(BlockGroup group, IReadOnlyList<GraphBlockEntry> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        Group = group;
        Blocks = blocks;
    }

    /// <summary>The group.</summary>
    public BlockGroup Group { get; }

    /// <summary>Its blocks, ordered by title.</summary>
    public IReadOnlyList<GraphBlockEntry> Blocks { get; }
}
