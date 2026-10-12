using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// The blocks and events this build registers (spec §2, W1): descriptors by id and event descriptors by kind, built
/// from the AddGate/AddStep/AddEffect/AddEvidence/AddEvent registrations. A singleton; the validator reads only this.
/// </summary>
public sealed class BlockCatalog
{
    private readonly Dictionary<BlockId, BlockDescriptor> _blocks = [];
    private readonly Dictionary<EventKind, EventDescriptor> _events = [];

    /// <summary>Builds the catalog. A duplicate id or kind, or a descriptor whose kind isn't its id's prefix, is a registration bug and throws.</summary>
    /// <param name="blocks">Every registered block descriptor.</param>
    /// <param name="events">Every registered event descriptor.</param>
    public BlockCatalog(IEnumerable<BlockDescriptor> blocks, IEnumerable<EventDescriptor> events)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(events);
        foreach (var block in blocks)
        {
            if (BlockKinds.Of(block.Id) != block.Kind)
            {
                throw new InvalidOperationException($"Block {block.Id.Value} is declared as {block.Kind}, but its prefix names {BlockKinds.Of(block.Id)}.");
            }

            if (!_blocks.TryAdd(block.Id, block))
            {
                throw new InvalidOperationException($"Block {block.Id.Value} is registered more than once.");
            }
        }

        foreach (var e in events)
        {
            if (!_events.TryAdd(e.Kind, e))
            {
                throw new InvalidOperationException($"Event {e.Kind.Value} is registered more than once.");
            }
        }
    }

    /// <summary>An empty catalog.</summary>
    public static BlockCatalog Empty { get; } = new([], []);

    /// <summary>Every block descriptor.</summary>
    public IReadOnlyCollection<BlockDescriptor> Blocks => _blocks.Values;

    /// <summary>Every event descriptor.</summary>
    public IReadOnlyCollection<EventDescriptor> Events => _events.Values;

    /// <summary>The descriptor of <paramref name="id"/>.</summary>
    /// <param name="id">A block id.</param>
    /// <returns>The descriptor, or null when no block has that id (RCP-BLOCK-001).</returns>
    public BlockDescriptor? Find(BlockId id) => _blocks.GetValueOrDefault(id);

    /// <summary>The descriptor of the event kind <paramref name="kind"/>.</summary>
    /// <param name="kind">An event kind.</param>
    /// <returns>The descriptor, or null when no event has that kind (RCP-EVENT-001).</returns>
    public EventDescriptor? FindEvent(EventKind kind) => _events.GetValueOrDefault(kind);
}
