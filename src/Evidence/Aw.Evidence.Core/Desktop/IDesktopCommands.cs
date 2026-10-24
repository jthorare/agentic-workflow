using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Runs the repo's own fixture command and the UI driver's command line (arguments, never a shell).</summary>
public interface IDesktopCommands
{
    /// <summary>Runs <paramref name="command"/> to completion.</summary>
    /// <param name="command">The program, its working directory, its extra environment and its time limit.</param>
    /// <param name="cancellationToken">Cancels the run (the process is killed).</param>
    /// <returns>How it ended, or AW-EVIDENCE-015 when it cannot start or runs out of time.</returns>
    public ValueTask<Result<CommandResult>> RunAsync(CommandSpec command, CancellationToken cancellationToken);
}
