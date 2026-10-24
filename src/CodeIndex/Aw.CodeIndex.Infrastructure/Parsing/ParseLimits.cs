using Aw.CodeIndex.Core;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>The guards of one parse (spec §9 CI12): how deep a tree may nest, how long a file may take, how big the parse thread's stack is, and the per-file ceilings.</summary>
/// <param name="MaxDepth">The deepest syntax nesting the walker follows; a deeper tree ends 'too-deep'.</param>
/// <param name="TimeLimit">How long one file's parse may run; past it the file ends 'timeout'.</param>
/// <param name="StackBytes">The stack of the thread a parse runs on.</param>
/// <param name="MaxSymbols">Most symbols a file may hold.</param>
/// <param name="MaxCallSites">Most call sites a file may hold.</param>
internal sealed record ParseLimits(int MaxDepth, TimeSpan TimeLimit, int StackBytes, int MaxSymbols, int MaxCallSites)
{
    /// <summary>The limits a real build runs with.</summary>
    public static ParseLimits Default { get; } = new(
        MaxDepth: 1_000,
        TimeLimit: TimeSpan.FromSeconds(60),
        StackBytes: 64 * 1024 * 1024,
        MaxSymbols: CodeIndexRules.MaxSymbolsPerFile,
        MaxCallSites: CodeIndexRules.MaxCallSitesPerFile);
}
