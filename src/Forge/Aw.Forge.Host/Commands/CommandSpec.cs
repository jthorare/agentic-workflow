namespace Aw.Forge.Host.Commands;

/// <summary>What a command accepts: its options with values, its boolean flags, its positional arguments and whether a "--" command follows.</summary>
/// <param name="Usage">The command's usage line.</param>
/// <param name="Required">Options that must be given.</param>
/// <param name="Optional">Options that may be given.</param>
/// <param name="Flags">Boolean flags.</param>
/// <param name="Positionals">How many positional arguments it takes.</param>
/// <param name="TakesCommand">Whether "--" and a command follow.</param>
internal sealed record CommandSpec(string Usage, string[] Required, string[] Optional, string[] Flags, int Positionals = 0, bool TakesCommand = false);
