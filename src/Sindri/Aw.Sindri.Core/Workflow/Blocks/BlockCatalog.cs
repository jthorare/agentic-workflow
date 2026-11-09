using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// The block descriptors this build loaded (today the skill blocks) by id, and the ids that could not be loaded with the reason. The agent runtime looks a Step's block up here.
/// The recipe engine's event and sentence registries went with it.
/// </summary>
public sealed class BlockCatalog
{
    private readonly Dictionary<BlockId, BlockDescriptor> _blocks = [];
    private readonly Dictionary<BlockId, string> _unloaded = [];

    /// <summary>Makes a catalog.</summary>
    /// <param name="blocks">The blocks; two with one id is an error.</param>
    /// <param name="unloaded">The ids that were skipped when loading, with why.</param>
    public BlockCatalog(IEnumerable<BlockDescriptor> blocks, IEnumerable<UnloadedBlock>? unloaded = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        foreach (var block in blocks)
        {
            if (!_blocks.TryAdd(block.Id, block))
            {
                throw new InvalidOperationException($"Block {block.Id.Value} is registered more than once.");
            }
        }

        foreach (var gone in unloaded ?? [])
        {
            _ = _unloaded.TryAdd(gone.Id, gone.Reason);
        }
    }

    /// <summary>A catalog with no block.</summary>
    public static BlockCatalog Empty { get; } = new([]);

    /// <summary>Every block.</summary>
    public IReadOnlyCollection<BlockDescriptor> Blocks => _blocks.Values;

    /// <summary>Finds a block.</summary>
    /// <param name="id">The block id.</param>
    /// <returns>The descriptor, or null when this build has no such block.</returns>
    public BlockDescriptor? Find(BlockId id) => _blocks.GetValueOrDefault(id);

    /// <summary>Why a block is not loaded.</summary>
    /// <param name="id">The block id.</param>
    /// <returns>The reason, or null when it was not skipped.</returns>
    public string? WhyUnloaded(BlockId id) => _unloaded.GetValueOrDefault(id);
}
