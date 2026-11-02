namespace Aw.Judge.Core.Processes;

/// <summary>One program run: what to start, with what input, where, and for how long at most.</summary>
public sealed record ProcessRequest
{
    /// <summary>The program: a bare name is looked up on the search path, a path is used as is.</summary>
    public required string FileName { get; init; }

    /// <summary>The arguments, each passed as one argv entry (no shell quoting).</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Written to the program's standard input, which is then closed; null closes it at once.</summary>
    public string? StandardInput { get; init; }

    /// <summary>The working directory; null for the caller's.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Variables added to (or replacing in) the inherited environment.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Inherited variables the program must not see (applied after <see cref="Environment"/>).</summary>
    public IReadOnlyCollection<string> Unset { get; init; } = [];

    /// <summary>
    /// When set, the program starts with only these variables of the inherited environment (those the caller has), then
    /// <see cref="Environment"/> is added and <see cref="Unset"/> applied; null inherits everything.
    /// </summary>
    public IReadOnlyCollection<string>? InheritOnly { get; init; }

    /// <summary>
    /// How many characters of each output stream are kept; the rest is read and dropped, so a program that floods its
    /// output can't grow the caller's memory without bound.
    /// </summary>
    public int OutputLimit { get; init; } = DefaultOutputLimit;

    /// <summary>The default <see cref="OutputLimit"/>: 4 Mi characters.</summary>
    public const int DefaultOutputLimit = 4 << 20;

    /// <summary>How long the program may run before it and every process it started are stopped.</summary>
    public required TimeSpan Timeout { get; init; }
}
