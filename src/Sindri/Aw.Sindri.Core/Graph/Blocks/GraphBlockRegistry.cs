namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The blocks this build has, by type name: the catalog the validator reads and the engine runs from.</summary>
public sealed class GraphBlockRegistry : IGraphBlockCatalog
{
    private readonly Dictionary<string, IGraphBlock> _blocks = new(StringComparer.Ordinal);

    /// <summary>Makes a registry of <paramref name="blocks"/>.</summary>
    /// <param name="blocks">The blocks; two with one type name is an error.</param>
    public GraphBlockRegistry(IEnumerable<IGraphBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        foreach (var block in blocks)
        {
            if (!_blocks.TryAdd(block.Type, block))
            {
                throw new ArgumentException($"two blocks are named '{block.Type}'", nameof(blocks));
            }
        }

        Types = _blocks.ToDictionary(static p => p.Key, static p => (IGraphBlockType)p.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IGraphBlockType> Types { get; }

    /// <summary>The blocks of PR 6 (schedule, manual, condition, notify and prompt) and of PR 9 so far (for_each and collect).</summary>
    /// <param name="notify">Where 'notify' writes.</param>
    /// <param name="model">What 'prompt' asks.</param>
    /// <param name="forEachLimits">Where 'for_each' reads its defaults; null takes <see cref="ForEachLimits.Defaults"/>.</param>
    /// <returns>The registry.</returns>
    public static GraphBlockRegistry Standard(INotifyPort notify, IGraphModelPort model, IForEachLimitSource? forEachLimits = null) =>
        new([
            new ScheduleBlock(), new ManualBlock(), new ConditionBlock(), new NotifyBlock(notify), new PromptBlock(model),
            new ForEachBlock(forEachLimits ?? new DefaultForEachLimits()), new CollectBlock(),
        ]);

    /// <summary>Finds the block of a type.</summary>
    /// <param name="type">The type name, such as 'condition'.</param>
    /// <returns>The block, or null when this build has no such block.</returns>
    public IGraphBlock? Find(string type) => _blocks.GetValueOrDefault(type);
}
