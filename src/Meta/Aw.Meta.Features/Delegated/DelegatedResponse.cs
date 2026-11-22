using Aw.Meta.Core.Delegations;


namespace Aw.Meta.Features.Delegated;

/// <summary>The Delegated response.</summary>
/// <param name="Delegations">The delegations, oldest first, each with its state.</param>
public sealed record DelegatedResponse(IReadOnlyList<DelegationView> Delegations);
