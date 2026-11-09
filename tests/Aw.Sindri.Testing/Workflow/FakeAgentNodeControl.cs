using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// An <see cref="IAgentNodeControl"/> with no Temporal: one 'run_agent' node a run waits on (set with <see cref="Wait"/>), moved by a takeover and a handback as the workflow moves it, and a log of the calls it got.
/// </summary>
public sealed class FakeAgentNodeControl : IAgentNodeControl
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private RunningAgentNode? _waiting;

    /// <summary>The calls it got, in order: 'takeover:&lt;run&gt;:&lt;node&gt;:&lt;index&gt;' and 'handback:…'.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>When set, every call fails with this error (Temporal down).</summary>
    public Error? Fail { get; set; }

    /// <summary>Makes the run wait on this node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item's position.</param>
    /// <param name="takenOver">Whether a person already holds it.</param>
    public void Wait(NodeId node, int itemIndex = 0, bool takenOver = false)
    {
        lock (_gate)
        {
            _waiting = new RunningAgentNode(node, itemIndex, LaunchId.From("LCH-00000000000000000000000000000001"), takenOver);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<RunningAgentNode?>> Running(WorkflowRunId run, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(Fail is { } error ? (Result<RunningAgentNode?>)error : _waiting);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Takeover(WorkflowRunId run, NodeId node, int itemIndex, CancellationToken cancellationToken) => Move("takeover", run, node, itemIndex, taken: true);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Handback(WorkflowRunId run, NodeId node, int itemIndex, CancellationToken cancellationToken) => Move("handback", run, node, itemIndex, taken: false);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Report(WorkflowRunId run, AgentNodeReport report, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    private ValueTask<Result<Unit>> Move(string verb, WorkflowRunId run, NodeId node, int itemIndex, bool taken)
    {
        lock (_gate)
        {
            _calls.Add(string.Create(CultureInfo.InvariantCulture, $"{verb}:{run.Value}:{node.Value}:{itemIndex}"));
            if (Fail is { } error)
            {
                return ValueTask.FromResult<Result<Unit>>(error);
            }

            if (_waiting is { } now && now.Node == node && now.ItemIndex == itemIndex)
            {
                _waiting = now with { TakenOver = taken };
            }

            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }
}
