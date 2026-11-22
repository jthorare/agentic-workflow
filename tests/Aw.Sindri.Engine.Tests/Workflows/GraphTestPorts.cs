using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The write ports of the walk tests: each one counts the writes it saw, so a test run can show it made none.</summary>
internal sealed class GraphTestPorts : INotifyPort, ICreateItemPort
{
    private readonly ConcurrentQueue<string> _notified = new();
    private readonly ConcurrentQueue<string> _created = new();

    /// <summary>The title of each notification written, in order.</summary>
    public IReadOnlyList<string> Notified => [.. _notified];

    /// <summary>The title of each item made, in order.</summary>
    public IReadOnlyList<string> Created => [.. _created];

    public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken)
    {
        _notified.Enqueue(title);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<Result<ItemMade>> Create(ItemToCreate item, CancellationToken cancellationToken)
    {
        _created.Enqueue(item.Title);
        return ValueTask.FromResult<Result<ItemMade>>(new ItemMade(ItemId.From("ITM-" + new string('c', 32)), Warning: null));
    }
}
