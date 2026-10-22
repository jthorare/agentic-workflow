using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Testing.Checks;

/// <summary>
/// The in-memory <see cref="ICheckCommands"/>: a command answers with the run a test gave its name, or an error when it has none (as a program that is not installed does).
/// Every call is kept. Held to <see cref="CheckCommandsContract"/>.
/// </summary>
public sealed class InMemoryCheckCommands : ICheckCommands
{
    private readonly Dictionary<string, Func<string, CheckRun>> _answers = new(StringComparer.Ordinal);
    private readonly List<(CheckInvocation Command, string WorkingDirectory)> _calls = [];

    /// <summary>Every call so far, in order.</summary>
    public IReadOnlyList<(CheckInvocation Command, string WorkingDirectory)> Calls => _calls;

    /// <summary>Makes the command named <paramref name="name"/> end the same way every time.</summary>
    /// <param name="name">The command's name.</param>
    /// <param name="run">How it ends.</param>
    /// <returns>The same instance.</returns>
    public InMemoryCheckCommands Answer(string name, CheckRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _answers[name] = _ => run;
        return this;
    }

    /// <summary>Makes the command named <paramref name="name"/> end by what it was given: the worktree it ran in.</summary>
    /// <param name="name">The command's name.</param>
    /// <param name="run">How it ends, from the working directory.</param>
    /// <returns>The same instance.</returns>
    public InMemoryCheckCommands Answer(string name, Func<string, CheckRun> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _answers[name] = run;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<CheckRun>> Run(CheckInvocation command, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add((command, workingDirectory));
        return ValueTask.FromResult<Result<CheckRun>>(_answers.TryGetValue(command.Name, out var answer)
            ? answer(workingDirectory)
            : SindriErrors.InvalidInput.With($"{command.Argv[0]} can't be started"));
    }
}
