namespace Aw.Meta.Host.Commands;

/// <summary>What a command accepts. Every command also takes the --json flag.</summary>
/// <param name="Usage">The command's usage line.</param>
/// <param name="Required">Options with a value that must be given.</param>
/// <param name="Optional">Options with a value that may be given.</param>
/// <param name="Flags">Boolean flags (besides --json).</param>
/// <param name="Positionals">How many positional arguments it takes (at least, with <paramref name="AnyMore"/>).</param>
/// <param name="AnyMore">Whether further positional arguments are accepted after those.</param>
internal sealed record CommandSpec(string Usage, string[] Required, string[] Optional, string[] Flags, int Positionals = 0, bool AnyMore = false)
{
    internal const string JsonFlag = "json";

    /// <summary>Options with a value that may be given more than once (delegate --label a --label b).</summary>
    internal string[] Repeated { get; init; } = [];

    internal static CommandSpec None(string usage) => new(usage, [], [], []);
}
