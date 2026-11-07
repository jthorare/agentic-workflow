namespace Aw.Sindri.Core.Reviews;

/// <summary>One design-doc ref a PR's Covers: line names, and whether it resolves now.</summary>
/// <param name="Ref">The ref as written.</param>
/// <param name="Resolved">Whether the library resolves it.</param>
public sealed record ReviewClaim(string Ref, bool Resolved);
