using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Testing.Toolkits;

/// <summary>IToolkitCalls in memory. Held to <see cref="ToolkitCallsContract"/>.</summary>
public sealed class InMemoryToolkitCalls : IToolkitCalls
{
    private readonly Lock _gate = new();
    private readonly List<ToolkitCall> _calls = [];

    /// <summary>Gets every stored call, oldest first (for assertions).</summary>
    public IReadOnlyList<ToolkitCall> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Add(ToolkitCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        lock (_gate)
        {
            _calls.Add(call);
        }

        return ValueTask.FromResult(Results.Success(Unit.Value));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<ToolkitCall>>> List(string? toolkit, int limit, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var rows = _calls.Select(static (c, i) => (Call: c, Index: i))
                .Where(x => toolkit is null || string.Equals(x.Call.Toolkit, toolkit, StringComparison.Ordinal))
                .OrderByDescending(static x => x.Call.At).ThenByDescending(static x => x.Index)
                .Take(limit).Select(static x => x.Call).ToList();
            return ValueTask.FromResult(Results.Success<IReadOnlyList<ToolkitCall>>(rows));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> DeleteBefore(DateTimeOffset before, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(Results.Success(_calls.RemoveAll(c => c.At < before)));
        }
    }
}
