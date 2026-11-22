namespace Aw.Evidence.Core.Stack;

/// <summary>
/// What counts as seed data: a database holding at least one profile whose email matches the pattern was seeded. A prod
/// dump restored locally has no such row, so it stays unknown. Unconfigured, the pattern matches only a fictional
/// example.com seed admin, so a database without one fails closed (provenance unknown, evidence stays local).
/// </summary>
/// <param name="EmailPattern">A LIKE pattern at least one seed profile matches.</param>
public sealed record SeedPolicy(string EmailPattern)
{
    /// <summary>The fail-closed default: 'seed-admin-%@example.com'.</summary>
    public static SeedPolicy Default { get; } = new("seed-admin-%@example.com");

    /// <summary>The policy from settings text (UI_EVIDENCE_SEED_EMAIL_PATTERN); blank keeps the default.</summary>
    /// <param name="emailPattern">The pattern setting.</param>
    /// <returns>The policy.</returns>
    public static SeedPolicy From(string? emailPattern) =>
        string.IsNullOrWhiteSpace(emailPattern) ? Default : new(emailPattern);
}
