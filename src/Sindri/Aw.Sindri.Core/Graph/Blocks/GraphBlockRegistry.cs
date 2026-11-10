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

    /// <summary>The blocks of PR 6 (schedule, manual, condition, notify and prompt), PR 7 (run_agent), PR 11 (connection_event), PR 9 loops (for_each and collect) and any <paramref name="more"/> a later PR adds (PR 9 flow blocks).</summary>
    /// <param name="notify">Where 'notify' writes.</param>
    /// <param name="model">What 'prompt' asks.</param>
    /// <param name="forEachLimits">Where 'for_each' reads its defaults; null takes <see cref="ForEachLimits.Defaults"/>.</param>
    /// <param name="more">Further blocks, such as <see cref="FlowBlocks"/>; null for none.</param>
    /// <returns>The registry.</returns>
    public static GraphBlockRegistry Standard(INotifyPort notify, IGraphModelPort model, IForEachLimitSource? forEachLimits = null, IEnumerable<IGraphBlock>? more = null) =>
        new([
            new ScheduleBlock(), new ConnectionEventBlock(), new ManualBlock(), new ConditionBlock(), new NotifyBlock(notify), new PromptBlock(model),
            new ForEachBlock(forEachLimits ?? new DefaultForEachLimits()), new CollectBlock(), new RunAgentBlock(),
            .. more ?? [],
        ]);

    /// <summary>The blocks of agent-first PR 9 part C: only_new, create_item, run_workflow, the workflow_call trigger and http.</summary>
    /// <param name="seen">What 'only_new' remembers.</param>
    /// <param name="items">Where 'create_item' makes the item.</param>
    /// <param name="calls">Where 'run_workflow' starts the other workflow.</param>
    /// <param name="http">Where 'http' sends its request.</param>
    /// <returns>The five blocks.</returns>
    public static IReadOnlyList<IGraphBlock> FlowBlocks(IGraphSeenStore seen, ICreateItemPort items, IGraphCallPort calls, IGraphHttpPort http) =>
        [new OnlyNewBlock(seen), new CreateItemBlock(items), new RunWorkflowBlock(calls), new WorkflowCallTrigger(), new HttpBlock(http)];

    /// <summary>The parking blocks of agent-first PR 9 part B: wait and ask_owner.</summary>
    /// <param name="questions">Where 'ask_owner' writes.</param>
    /// <param name="clock">The clock 'wait' and 'ask_owner' read.</param>
    /// <returns>The two blocks.</returns>
    public static IReadOnlyList<IGraphBlock> ParkingBlocks(IQuestionPort questions, TimeProvider clock) =>
        [new WaitBlock(clock), new AskOwnerBlock(questions, clock)];

    /// <summary>The 'workflow_result' block of agent-first PR 9: it reads a finished run's outputs.</summary>
    /// <param name="results">Where 'workflow_result' reads a run.</param>
    /// <returns>The block.</returns>
    public static IReadOnlyList<IGraphBlock> ResultBlocks(IGraphResultPort results) => [new WorkflowResultBlock(results)];

    /// <summary>Finds the block of a type.</summary>
    /// <param name="type">The type name, such as 'condition'.</param>
    /// <returns>The block, or null when this build has no such block.</returns>
    public IGraphBlock? Find(string type) => _blocks.GetValueOrDefault(type);
}
