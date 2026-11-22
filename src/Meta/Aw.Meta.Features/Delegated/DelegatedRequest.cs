namespace Aw.Meta.Features.Delegated;

/// <summary>The Delegated request: list the delegations.</summary>
/// <param name="All">True to list the answered ones too; by default only owed and sent ones are listed.</param>
public sealed record DelegatedRequest(bool All = false);
