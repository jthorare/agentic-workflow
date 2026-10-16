using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>A wake that reports the instances a test seeds and records each notice sent (<see cref="RecipeWakeContract"/>).</summary>
public sealed class RecordingRecipeWake : IRecipeWake
{
    private readonly Dictionary<RecipeHash, List<WorkflowInstanceId>> _running = [];
    private readonly List<RecipeWakeNotice> _sent = [];
    private readonly Lock _gate = new();

    /// <summary>The notices sent so far, oldest first.</summary>
    public IReadOnlyList<RecipeWakeNotice> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    /// <summary>The error every wake answers with while set (the Temporal side failing); null sends normally.</summary>
    public Error? Fail { get; set; }

    /// <summary>Seeds <paramref name="instance"/> as running <paramref name="hash"/>.</summary>
    /// <param name="hash">The recipe hash.</param>
    /// <param name="instance">The instance.</param>
    public void Run(RecipeHash hash, WorkflowInstanceId instance)
    {
        lock (_gate)
        {
            if (!_running.TryGetValue(hash, out var list))
            {
                _running[hash] = list = [];
            }

            list.Add(instance);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Running(RecipeHash hash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowInstanceId> found = _running.TryGetValue(hash, out var list) ? [.. list] : [];
            return ValueTask.FromResult(Results.Success(found));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Wake(RecipeWakeNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (Fail is { } failure)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        lock (_gate)
        {
            _sent.Add(notice);
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
