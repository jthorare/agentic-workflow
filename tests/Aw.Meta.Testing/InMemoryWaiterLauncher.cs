using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IWaiterLauncher: records each command it was asked to start and answers a fixed pid.</summary>
public sealed class InMemoryWaiterLauncher : IWaiterLauncher
{
    private readonly List<IReadOnlyList<string>> _started = [];

    /// <summary>The pid every start answers.</summary>
    public int Pid { get; set; } = 4242;

    /// <summary>The commands started, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Started => _started;

    /// <inheritdoc />
    public ValueTask<Result<int>> Start(IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argv);
        _started.Add([.. argv]);
        return Done.Of(Pid);
    }
}
