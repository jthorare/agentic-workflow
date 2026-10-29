using System.Diagnostics;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// bwrap --die-with-parent sets PR_SET_PDEATHSIG, which Linux ties to the thread that forked the child; a pool thread retires after about 20 s idle and takes the
/// sandboxed run with it (exit 137, #1680). So in every namespace that builds a --die-with-parent argv, a type that starts a process starts it through
/// Aw.Kernel.Processes.PinnedProcess. Read from the IL, so an async method (its state machine is a nested type) counts for its top-level type.
/// </summary>
public static class PinnedProcessRules
{
    /// <summary>A type next to a --die-with-parent argv builder calls Process.Start itself.</summary>
    public const string Id = "ARCH-PROC-002";

    private const string Marker = "--die-with-parent";

    /// <summary>Checks the types.</summary>
    /// <param name="types">Every type of the assemblies in scope.</param>
    /// <param name="exemptions">Full type name to the reason it may call Process.Start.</param>
    /// <returns>One violation per offending type.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> types, IReadOnlyDictionary<string, string> exemptions)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(exemptions);
        var topLevel = types.Where(static t => !t.IsNested && t.Name != "<Module>").ToList();
        var guarded = topLevel.Where(static t => IlScan.Literals(t).Any(static l => l.Contains(Marker, StringComparison.Ordinal)))
            .Select(static t => t.Namespace ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        return topLevel.Where(t => guarded.Contains(t.Namespace ?? string.Empty) && t.Name != "PinnedProcess" && !exemptions.ContainsKey(t.FullName ?? t.Name) && CallsProcessStart(t))
            .Select(static t => new Violation(Id, $"{t.FullName} calls Process.Start in a namespace that builds a {Marker} argv",
                $"start it with Aw.Kernel.Processes.PinnedProcess: bwrap {Marker} dies with the thread that forked it"));
    }

    private static bool CallsProcessStart(Type type) =>
        IlScan.CalledMethods(type).Any(static m => m.DeclaringType == typeof(Process) && m.Name == nameof(Process.Start));
}
