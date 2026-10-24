using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>A scripted IDesktopCommands: every command recorded, exit 0 and no output unless a rule says otherwise, programs that will not start.</summary>
public sealed class FakeDesktopCommands : IDesktopCommands
{
    private readonly List<CommandSpec> _calls = [];
    private readonly List<(Func<CommandSpec, bool> Matches, Func<CommandSpec, CommandResult> Answer)> _rules = [];

    /// <summary>Every command, in order.</summary>
    public IReadOnlyList<CommandSpec> Calls => _calls;

    /// <summary>Programs (the first element of a command) that will not start.</summary>
    public ISet<string> WontStart { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Answers each command <paramref name="matches"/> accepts with <paramref name="answer"/>; the last rule added that matches wins.</summary>
    /// <param name="matches">Which commands.</param>
    /// <param name="answer">What happens.</param>
    /// <returns>This fake.</returns>
    public FakeDesktopCommands When(Func<CommandSpec, bool> matches, Func<CommandSpec, CommandResult> answer)
    {
        _rules.Insert(0, (matches, answer));
        return this;
    }

    /// <summary>The commands as one line each ('prog arg arg'), in order.</summary>
    /// <returns>The lines.</returns>
    public IReadOnlyList<string> Lines() => [.. _calls.Select(static c => string.Join(' ', c.Argv))];

    /// <inheritdoc/>
    public ValueTask<Result<CommandResult>> RunAsync(CommandSpec command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(command);
        if (WontStart.Contains(command.Argv[0]))
        {
            return ValueTask.FromResult<Result<CommandResult>>(EvidenceErrors.RunFailed.With($"{command.Argv[0]} could not start"));
        }

        var (_, answer) = _rules.Find(r => r.Matches(command));
        return ValueTask.FromResult<Result<CommandResult>>(answer is null ? new CommandResult(0, string.Empty, string.Empty) : answer(command));
    }
}
