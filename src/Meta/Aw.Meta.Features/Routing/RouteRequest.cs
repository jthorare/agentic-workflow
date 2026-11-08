namespace Aw.Meta.Features.Routing;

/// <summary>The Route request: suggest which lanes should move to another provider, and with Apply write the move.</summary>
/// <param name="Apply">Whether to write the suggestions into routing.lanes of the team config.</param>
/// <param name="Lane">Limits the call to one lane; null means every lane.</param>
/// <param name="Force">Whether to apply a move to a provider whose usage is unknown.</param>
public sealed record RouteRequest(bool Apply = false, string? Lane = null, bool Force = false);
