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
    private readonly Dictionary<BlockId, string> _unloaded = [];

    /// <summary>Builds the catalog. A duplicate id or kind, or a descriptor whose kind isn't its id's prefix, is a registration bug and throws.</summary>
    /// <param name="blocks">Every registered block descriptor.</param>
    /// <param name="events">Every registered event descriptor.</param>
    /// <param name="sentences">The plain sentences; each descriptor without one of its own takes the sentence of its id. Null leaves every sentence as the descriptor has it.</param>
    /// <param name="unloaded">The blocks that did not load, with the reason (skill flows spec §3.2); null for none. An id that is also registered keeps its descriptor.</param>
    public BlockCatalog(IEnumerable<BlockDescriptor> blocks, IEnumerable<EventDescriptor> events, IBlockSentences? sentences = null, IEnumerable<UnloadedBlock>? unloaded = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(events);
        foreach (var described in blocks)
        {
            var block = described.Sentence is null && sentences?.For(described.Id) is { } sentence ? described with { Sentence = sentence } : described;
            if (BlockKinds.Of(block.Id) != block.Kind)
            {
                throw new InvalidOperationException($"Block {block.Id.Value} is declared as {block.Kind}, but its prefix names {BlockKinds.Of(block.Id)}.");
            }

            if (!_blocks.TryAdd(block.Id, block))
            {
                throw new InvalidOperationException($"Block {block.Id.Value} is registered more than once.");
            }
        }

        Remember(unloaded);
        foreach (var e in events)
        {
            if (!_events.TryAdd(e.Kind, e))
            {
                throw new InvalidOperationException($"Event {e.Kind.Value} is registered more than once.");
            }
        }
    }

    private void Remember(IEnumerable<UnloadedBlock>? unloaded)
    {
        foreach (var gone in unloaded ?? [])
        {
            _ = _unloaded.TryAdd(gone.Id, gone.Reason);
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

    /// <summary>Why <paramref name="id"/> did not load.</summary>
    /// <param name="id">A block id.</param>
    /// <returns>The reason, or null when the block is not one that failed to load.</returns>
    public string? WhyUnloaded(BlockId id) => _unloaded.GetValueOrDefault(id);

    /// <summary>The descriptor of the event kind <paramref name="kind"/>.</summary>
    /// <param name="kind">An event kind.</param>
    /// <returns>The descriptor, or null when no event has that kind (RCP-EVENT-001).</returns>
    public EventDescriptor? FindEvent(EventKind kind) => _events.GetValueOrDefault(kind);
}
