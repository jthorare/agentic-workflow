using System.Collections;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Desktop;

/// <summary>
/// Runs the repo's fixture command and the UI driver's command line as child processes: the arguments as given (never a shell), the working directory the repo, the
/// environment inherited plus the run's variables. A program with a directory in its name (scripts/fixture.sh) is the working directory's; a bare
/// name (dotnet) is looked up on PATH.
/// </summary>
internal sealed class ProcessDesktopCommands : IDesktopCommands
{
    /// <inheritdoc/>
    public async ValueTask<Result<CommandResult>> RunAsync(CommandSpec command, CancellationToken cancellationToken)
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = entry.Value as string ?? string.Empty;
        }

        foreach (var (name, value) in command.Environment)
        {
            environment[name] = value;
        }

        var argv = new List<string>(command.Argv) { [0] = ProgramOf(command) };
        var outcome = await ProcessRunner.RunAsync(new ProcessSpec(argv, StandardInput: null, environment, command.WorkingDirectory), command.Timeout, EvidenceErrors.RunFailed, cancellationToken).ConfigureAwait(false);
        return outcome.Map(static o => new CommandResult(o.ExitCode, o.StandardOutput, o.StandardError));
    }

    private static string ProgramOf(CommandSpec command)
    {
        var program = command.Argv[0];
        return program.Contains('/', StringComparison.Ordinal) && !program.StartsWith('/') ? Path.Combine(command.WorkingDirectory, program) : program;
    }
}
