using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>The runtime the MCP endpoint and the 'call_tool' block see (<see cref="IRunningToolkits"/>): the supervisor's live processes, with every call written to the call log.</summary>
/// <param name="supervisor">Owns the processes.</param>
/// <param name="store">Tells why a toolkit that isn't running can't be called.</param>
/// <param name="log">Takes one row per call.</param>
/// <param name="clock">Times the calls.</param>
internal sealed class RunningToolkits(ToolkitSupervisor supervisor, IToolkitStore store, ToolkitCallLog log, TimeProvider clock) : IRunningToolkits
{
    internal ToolkitSupervisor Supervisor { get; } = supervisor;

    internal IToolkitStore Store { get; } = store;

    internal ToolkitCallLog Log { get; } = log;

    internal TimeProvider Clock { get; } = clock;

    public IReadOnlyList<Toolkit> Toolkits() => throw new NotSupportedException();

    public ValueTask<Result<string>> CallTool(string toolkit, string tool, string argumentsJson, string caller, CancellationToken cancellationToken) => throw new NotSupportedException();
}
