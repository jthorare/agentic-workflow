namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The tier-0 packages (deps.yaml tier: 0, by projectPrefix): domain-free building blocks without the package shape.
/// Core and Features may use Aw.Kernel only (ARCH-DIR-005). RegistryTests checks this list against deps.yaml.
/// </summary>
public static class TierZero
{
    /// <summary>The one tier-0 assembly Core and Features may use.</summary>
    public const string Kernel = "Aw.Kernel";

    /// <summary>The tier-0 project prefixes; a project or assembly with one of them (or a further segment) is tier 0.</summary>
    public static IReadOnlyList<string> Prefixes { get; } = ["Aw.Kernel", "Aw.Redaction", "Aw.Analyzers", "Aw.Embeddings"];

    /// <summary>Whether a project or assembly name belongs to a tier-0 package.</summary>
    /// <param name="name">A project or assembly name.</param>
    /// <returns>True for tier 0.</returns>
    public static bool Contains(string name) =>
        Prefixes.Any(p => name == p || name.StartsWith(p + ".", StringComparison.Ordinal));
}
