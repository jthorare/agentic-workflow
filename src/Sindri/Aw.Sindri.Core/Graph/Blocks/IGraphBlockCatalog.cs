namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The block types this build has, by name. PR 5 ships the contract and an empty catalog; the blocks arrive with the engine (PR 6, 9).</summary>
public interface IGraphBlockCatalog
{
    /// <summary>The block types by their <see cref="IGraphBlockType.Type"/>.</summary>
    public IReadOnlyDictionary<string, IGraphBlockType> Types { get; }
}
