using Aw.Kernel;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// Runs a repo command in a worktree (line spec §7.2, LN-WF9): the port the build and ship Steps run the red tests, the build checks and the GREEN gate through. A command that
/// fails or runs out of time is an answer; an error means it could not be started (no such program, no such directory). Taking a heavy slot is the caller's: this port only runs.
/// </summary>
public interface ICheckCommands
{
    /// <summary>Runs <paramref name="command"/> with <paramref name="workingDirectory"/> as its working directory and waits for it.</summary>
    /// <param name="command">The command and its timeout.</param>
    /// <param name="workingDirectory">The worktree it runs in.</param>
    /// <param name="cancellationToken">Cancels the run: the command and its children are killed, and the call throws.</param>
    /// <returns>How it ended; an error when it could not be started.</returns>
    public ValueTask<Result<CheckRun>> Run(CheckInvocation command, string workingDirectory, CancellationToken cancellationToken);
}
