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

    /// <summary>The six blocks: schedule, connection_event (PR 11), manual, condition, notify and prompt, and any <paramref name="more"/> a later PR adds.</summary>
    /// <param name="notify">Where 'notify' writes.</param>
    /// <param name="model">What 'prompt' asks.</param>
    /// <param name="more">Further blocks, such as <see cref="FlowBlocks"/>; null for none.</param>
    /// <returns>The registry.</returns>
    public static GraphBlockRegistry Standard(INotifyPort notify, IGraphModelPort model, IEnumerable<IGraphBlock>? more = null) =>
        new([new ScheduleBlock(), new ConnectionEventBlock(), new ManualBlock(), new ConditionBlock(), new NotifyBlock(notify), new PromptBlock(model), .. more ?? []]);

    /// <summary>The blocks of agent-first PR 9 part C: only_new, create_item, run_workflow, the workflow_call trigger and http.</summary>
    /// <param name="seen">What 'only_new' remembers.</param>
    /// <param name="items">Where 'create_item' makes the item.</param>
    /// <param name="calls">Where 'run_workflow' starts the other workflow.</param>
    /// <param name="http">Where 'http' sends its request.</param>
    /// <returns>The five blocks.</returns>
    public static IReadOnlyList<IGraphBlock> FlowBlocks(IGraphSeenStore seen, ICreateItemPort items, IGraphCallPort calls, IGraphHttpPort http) =>
        [new OnlyNewBlock(seen), new CreateItemBlock(items), new RunWorkflowBlock(calls), new WorkflowCallTrigger(), new HttpBlock(http)];

    /// <summary>Finds the block of a type.</summary>
    /// <param name="type">The type name, such as 'condition'.</param>
    /// <returns>The block, or null when this build has no such block.</returns>
    public IGraphBlock? Find(string type) => _blocks.GetValueOrDefault(type);
}
