using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IProcessLiveness: the one fake, kept honest by ProcessLivenessContract.</summary>
public sealed class InMemoryProcessLiveness : IProcessLiveness
{
    private readonly Dictionary<int, DateTimeOffset> _running = [];
    private readonly Dictionary<int, Error> _pidFailures = [];
    private Error? _failure;

    /// <summary>How many times <see cref="IsRunning"/> was called directly (a batch does not count).</summary>
    public int SingleCalls { get; private set; }

    /// <summary>How many times <see cref="Running"/> was called.</summary>
    public int BatchCalls { get; private set; }

    /// <summary>Makes a pid a running process that started at the given time.</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="startedAt">When it started.</param>
    public void Run(int pid, DateTimeOffset startedAt) => _running[pid] = startedAt;

    /// <summary>Ends a process.</summary>
    /// <param name="pid">The process id.</param>
    public void Stop(int pid) => _ = _running.Remove(pid);

    /// <summary>Makes the answer for one pid fail, as a host that can't be asked about that process does.</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="error">The error.</param>
    public void FailWith(int pid, Error error) => _pidFailures[pid] = error;

    /// <summary>Makes every call fail, as a host that can't be asked does.</summary>
    /// <param name="error">The error.</param>
    public void FailWith(Error error) => _failure = error;

    /// <inheritdoc />
    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        SingleCalls++;
        return Answer(pid, recordedAt);
    }

    private ValueTask<Result<bool>> Answer(int pid, DateTimeOffset recordedAt) =>
        FailureFor(pid) is { } error
            ? Done.Failed<bool>(error)
            : Done.Of(pid > 0 && _running.TryGetValue(pid, out var startedAt) && !ProcessStart.IsReused(startedAt, recordedAt));

    private Error? FailureFor(int pid) => _failure ?? (_pidFailures.TryGetValue(pid, out var error) ? error : null);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<bool>>> Running(IReadOnlyList<ProcessClaim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        BatchCalls++;
        var answers = new List<Result<bool>>(claims.Count);
        foreach (var claim in claims)
        {
            answers.Add(await Answer(claim.Pid, claim.RecordedAt));
        }

        return answers;
    }
}
