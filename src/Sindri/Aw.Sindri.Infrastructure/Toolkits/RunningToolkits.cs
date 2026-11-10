using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>The runtime the MCP endpoint and the 'call_tool' block see (<see cref="IRunningToolkits"/>): the supervisor's live processes, with every call that reaches one written to the call log.</summary>
/// <param name="supervisor">Owns the processes.</param>
/// <param name="store">Tells why a toolkit that isn't running can't be called.</param>
/// <param name="log">Takes one row per call.</param>
/// <param name="clock">Times the calls.</param>
internal sealed class RunningToolkits(ToolkitSupervisor supervisor, IToolkitStore store, ToolkitCallLog log, TimeProvider clock) : IRunningToolkits
{
    public IReadOnlyList<Toolkit> Toolkits() => supervisor.RunningNow();

    public async ValueTask<Result<string>> CallTool(string toolkit, string tool, string argumentsJson, string caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        var live = supervisor.RunningNow().FirstOrDefault(t => string.Equals(t.Name, toolkit, StringComparison.Ordinal));
        if (live is null)
        {
            return await NotRunning(toolkit, cancellationToken).ConfigureAwait(false);
        }

        if (live.State == ToolkitState.Paused)
        {
            return Paused(toolkit);
        }

        var startedAt = clock.GetUtcNow();
        var began = clock.GetTimestamp();
        var answer = await supervisor.Call(toolkit, tool, argumentsJson, ToolkitMcpClient.DefaultCallTimeout, cancellationToken).ConfigureAwait(false);
        var took = clock.GetElapsedTime(began);
        var error = answer.IsError ? $"{answer.Error.Code} {answer.Error.Message}" : null;
        _ = await log.Record(new ToolkitCallEntry(toolkit, tool, caller, argumentsJson, answer.IsError ? null : answer.Value, error, took, startedAt), CancellationToken.None).ConfigureAwait(false);   // why: a log that can't write must not fail the call; a cancelled call is still logged
        return answer;
    }

    private async ValueTask<Result<string>> NotRunning(string toolkit, CancellationToken cancellationToken)
    {
        var found = await store.Find(toolkit, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value switch
        {
            null => SindriErrors.ToolkitNotFound.With($"No toolkit named '{toolkit}' (fix: toolkit_list names them)"),
            { State: ToolkitState.Paused } => Paused(toolkit),
            _ => SindriErrors.ToolkitNotBuilt.With($"Toolkit '{toolkit}' isn't running (fix: toolkit_start)"),
        };
    }

    private static Error Paused(string toolkit) => SindriErrors.ToolkitPaused.With($"Toolkit '{toolkit}' is paused (fix: toolkit_resume)");
}
