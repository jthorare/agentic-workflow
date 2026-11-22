using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The fail-closed fence of a test run (spec 2026-11-12 §5.1): every type a block's constructor takes either has a test version
/// (<see cref="Bound"/>, the wrappers of <see cref="GraphFence"/>) or is read-only and runs for real (<see cref="ReadOnly"/>). A type on neither list fails the architecture test.
/// </summary>
public static class GraphTestBindings
{
    /// <summary>The types that have a test version: each is wrapped by <see cref="GraphFence"/>.</summary>
    public static IReadOnlySet<Type> Bound { get; } = new HashSet<Type>
    {
        typeof(INotifyPort),
        typeof(ICreateItemPort),
        typeof(IQuestionPort),
        typeof(IGraphHttpPort),
        typeof(IGraphMemoryPort),
        typeof(IGraphSeenStore),
        typeof(IGraphModelPort),
        typeof(IGraphCallPort),
        typeof(IConnectionToolPort),
        typeof(IRunningToolkits),
    };

    /// <summary>The types that are read-only and run for real in a test: the clock, the sandboxed code runner, the result reader and the loop limits.</summary>
    public static IReadOnlySet<Type> ReadOnly { get; } = new HashSet<Type>
    {
        typeof(TimeProvider),
        typeof(ICodeRunPort),
        typeof(IGraphResultPort),
        typeof(IForEachLimitSource),
    };
}
