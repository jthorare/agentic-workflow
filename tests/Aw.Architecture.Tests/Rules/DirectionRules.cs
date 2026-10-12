namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The dependency direction inside and between packages (spec §4b.1: Host → Features → Core ← Infrastructure;
/// another package only through its Core).
/// </summary>
public static class DirectionRules
{
    /// <summary>Core depends on nothing else in its package.</summary>
    public const string CoreId = "ARCH-DIR-001";

    /// <summary>Features (App.Features) depends only on Core and Features.</summary>
    public const string FeaturesId = "ARCH-DIR-002";

    /// <summary>Infrastructure (App.Infrastructure) depends only on Core and Infrastructure.</summary>
    public const string InfrastructureId = "ARCH-DIR-003";

    /// <summary>Another package is used only through its Core.</summary>
    public const string CrossPackageId = "ARCH-DIR-004";

    /// <summary>Core and Features use Aw.Kernel and no other tier-0 package.</summary>
    public const string TierZeroId = "ARCH-DIR-005";

    /// <summary>Checks every edge against the direction rules, with no compose edges declared.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>One violation per offending edge.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) => Check(edges, []);

    /// <summary>
    /// Checks every edge against the direction rules. A Host's use of a compose target's Features or Infrastructure is
    /// the ARCH-DIR-004 carve-out: ARCH-COMPOSE-001 (<see cref="ComposeRules"/>) judges it instead.
    /// </summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <param name="compose">The declared compose edges (deps.yaml).</param>
    /// <returns>One violation per offending edge.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges, IReadOnlyCollection<ComposeEdge> compose) =>
        edges.Select(e => e switch
        {
            { From: { } from, To: { } to } => ComposeRules.Covers(e, compose) ? null : Judge(e, from, to),
            { From.Layer: Layer.Core or Layer.Features, To: null } when IsOtherTierZero(e) => new Violation(TierZeroId,
                $"{e.Text}: {e.From.Layer} uses {TypeNames.AssemblyOf(e.Target)}",
                "Core and Features use Aw.Kernel only; reach Aw.Kernel.Data, Aw.Kernel.Telemetry or Redaction through a Core port that Infrastructure implements"),
            _ => null,
        }).OfType<Violation>();

    // A tier-0 package other than Aw.Kernel (TierZero, checked against deps.yaml).
    private static bool IsOtherTierZero(Edge e)
    {
        var target = TypeNames.AssemblyOf(e.Target);
        return TierZero.Contains(target) && target != TierZero.Kernel;
    }

    private static Violation? Judge(Edge e, Place from, Place to)
    {
        if (from.Root != to.Root)
        {
            return to.Layer == Layer.Core ? null : new Violation(CrossPackageId,
                $"{e.Text} reaches package {to.Root} past its Core ({to.Layer})",
                $"depend on {to.Root}.Core only; move what you need into {to.Root}'s Core contracts, or call its CLI");
        }

        return (from.Layer, to.Layer) switch
        {
            (Layer.Core, not Layer.Core) => new Violation(CoreId, $"{e.Text}: Core depends on {to.Layer}",
                "Core depends on nothing else in its package; declare a port in Core and implement it in Infrastructure"),
            (Layer.Features, not (Layer.Core or Layer.Features)) => new Violation(FeaturesId, $"{e.Text}: Features depends on {to.Layer}",
                "Features uses Core types and ports only; move the dependency behind a Core port and register its adapter in Composition"),
            (Layer.Infrastructure, not (Layer.Core or Layer.Infrastructure)) => new Violation(InfrastructureId, $"{e.Text}: Infrastructure depends on {to.Layer}",
                "Infrastructure implements Core ports and returns Core types; move the shared type into Core"),
            _ => null,
        };
    }
}
