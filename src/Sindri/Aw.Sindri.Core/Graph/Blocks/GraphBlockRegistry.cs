namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The blocks this build has, by type name: the catalog the validator reads and the engine runs from.</summary>
public sealed class GraphBlockRegistry : IGraphBlockCatalog
{
    /// <summary>Makes a registry of <paramref name="blocks"/>.</summary>
    /// <param name="blocks">The blocks; two with one type name is an error.</param>
    public GraphBlockRegistry(IEnumerable<IGraphBlock> blocks)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IGraphBlockType> Types => throw new NotSupportedException();

    /// <summary>The five blocks of PR 6: schedule, manual, condition, notify and prompt.</summary>
    /// <param name="notify">Where 'notify' writes.</param>
    /// <param name="model">What 'prompt' asks.</param>
    /// <returns>The registry.</returns>
    public static GraphBlockRegistry Standard(INotifyPort notify, IGraphModelPort model) => throw new NotSupportedException();

    /// <summary>Finds the block of a type.</summary>
    /// <param name="type">The type name, such as 'condition'.</param>
    /// <returns>The block, or null when this build has no such block.</returns>
    public IGraphBlock? Find(string type) => throw new NotSupportedException();
}
