namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The fail-closed fence of a test run (spec 2026-11-12 §5.1): every type a block's constructor takes either has a test version
/// (<see cref="Bound"/>) or is read-only and runs for real (<see cref="ReadOnly"/>). A type on neither list is recorded, never run.
/// </summary>
public static class GraphTestBindings
{
    /// <summary>The types that have a test version, such as the notify and create-item ports. Empty until P4a is built.</summary>
    public static IReadOnlySet<Type> Bound { get; } = new HashSet<Type>();

    /// <summary>The types that are read-only and run for real in a test, such as the clock. Empty until P4a is built.</summary>
    public static IReadOnlySet<Type> ReadOnly { get; } = new HashSet<Type>();
}
