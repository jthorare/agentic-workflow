using System.Collections.Frozen;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge prompt-sort why|import|outcomes|adjudicate|eval|promote …': the sorter's by-hand side, not in this build yet;
/// cutover unit 6.4 replaces the body (and deletes a subcommand from <see cref="Subcommands"/> as it lands it).
/// </summary>
internal static class PromptSortNotBuiltCommand
{
    /// <summary>The stubbed subcommands.</summary>
    internal static readonly FrozenSet<string> Subcommands = FrozenSet.Create(StringComparer.Ordinal, "why", "import", "outcomes", "adjudicate", "eval", "promote");

    internal static int Run(string subcommand) => NotBuiltYet.Say($"{PromptSortCommands.Name} {subcommand}");
}
