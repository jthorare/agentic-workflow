namespace Aw.Meta.Core.Commands;

/// <summary>A command to run.</summary>
/// <param name="Argv">The program and its arguments.</param>
/// <param name="Timeout">How long it may run before it is killed.</param>
public sealed record CommandRequest(IReadOnlyList<string> Argv, TimeSpan Timeout)
{
    /// <summary>The working directory; null means the current one.</summary>
    public string? Cwd { get; init; }

    /// <summary>Environment variables to add or override.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Variables removed from the inherited environment before <see cref="Env"/> is applied.</summary>
    public IReadOnlyList<string> UnsetEnv { get; init; } = [];

    /// <summary>Whether to run with a fresh temporary HOME and AW_STATE_DIR (removed afterwards), as scripts/ci/bash-tests.sh gives each suite.</summary>
    public bool Isolated { get; init; }

    /// <summary>
    /// With <see cref="Isolated"/>: the index in <see cref="Argv"/> where the isolated command starts. Words before it (a wrapper such as the
    /// heavy-job script) run with the real environment; the rest run under "env" with the fresh HOME and AW_STATE_DIR. Zero isolates everything.
    /// </summary>
    public int IsolatedFrom { get; init; }

    /// <summary>A file the combined output is appended to as it arrives; null for none.</summary>
    public string? LogFile { get; init; }

    /// <summary>How many characters of each output stream the result keeps, counted from the end; the default is 64 KB. At least 1.</summary>
    public int TailCharacters
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = DefaultTailCharacters;

    /// <summary>The default for <see cref="TailCharacters"/>.</summary>
    public const int DefaultTailCharacters = 64 * 1024;

    /// <summary>How long the command gets to exit after SIGTERM before it and what it started are killed; null for the runner's default (45 seconds).</summary>
    public TimeSpan? TermGrace { get; init; }
}
