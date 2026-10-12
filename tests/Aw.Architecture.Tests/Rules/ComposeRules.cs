using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A Host uses a compose target's Features and Infrastructure only through their public static Add…() registration
/// methods (context spec §4): every other use of those projects goes through the target's Core. Without a compose edge
/// such a use is ARCH-DIR-004.
/// </summary>
public static class ComposeRules
{
    /// <summary>A Host using anything but an Add…() registration method of a compose target's Features or Infrastructure.</summary>
    public const string Id = "ARCH-COMPOSE-001";

    /// <summary>Checks every Host edge into a compose target's Features or Infrastructure.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <param name="compose">The declared compose edges.</param>
    /// <returns>One violation per offending edge.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges, IReadOnlyCollection<ComposeEdge> compose) =>
        edges.Where(e => Covers(e, compose)).Select(Judge).OfType<Violation>();

    /// <summary>Whether the edge is a Host's use of a compose target's Features or Infrastructure.</summary>
    /// <param name="e">The dependency.</param>
    /// <param name="compose">The declared compose edges.</param>
    /// <returns>True when ARCH-COMPOSE-001 judges it (and ARCH-DIR-004 doesn't).</returns>
    public static bool Covers(Edge e, IReadOnlyCollection<ComposeEdge> compose) =>
        e is { From: { Layer: Layer.Host } from, To: { Layer: Layer.Features or Layer.Infrastructure } to }
        && compose.Contains(new ComposeEdge(from.Root, to.Root));

    private static Violation? Judge(Edge e)
    {
        var fix = $"call only the public static Add…() registration methods of {e.To!.Root}'s {e.To.Layer} from the Host; reach everything else through {e.To.Root}.Core";
        if (e.Target is not Class { IsAbstract: true, IsSealed: true } || e.Target.Visibility != Visibility.Public)
        {
            return new Violation(Id, $"{e.Text}: the Host uses {e.To.Layer} type {e.Target.Name}, not a public static registration class", fix);
        }

        string[] other = [.. e.TargetMembers.Select(m => m.Split('(')[0]).Where(m => !IsRegistration(m)), .. e.TargetFields];
        return other.Length > 0 ? new Violation(Id, $"{e.Text}: calls {string.Join(", ", other)}", fix)
            : e.TargetMembers.Count == 0 ? new Violation(Id, $"{e.Text}: uses the class without calling an Add…() method", fix)
            : null;
    }

    // Add, or Add followed by an upper-case letter (AddContextFeatures, not Address).
    private static bool IsRegistration(string method) =>
        method.StartsWith("Add", StringComparison.Ordinal) && (method.Length == 3 || char.IsUpper(method[3]));
}
