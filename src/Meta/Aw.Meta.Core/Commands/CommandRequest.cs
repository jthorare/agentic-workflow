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

    /// <summary>A file the combined output is appended to as it arrives; null for none.</summary>
    public string? LogFile { get; init; }

    /// <summary>How many characters of each output stream the result keeps, counted from the end; the default is 64 KB.</summary>
    public int TailCharacters { get; init; } = DefaultTailCharacters;

    /// <summary>The default for <see cref="TailCharacters"/>.</summary>
    public const int DefaultTailCharacters = 64 * 1024;
}
