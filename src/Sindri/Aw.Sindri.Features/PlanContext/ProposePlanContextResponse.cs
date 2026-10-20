using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.PlanContext;

/// <summary>What the Step did: 'proposed' or 'skipped' (not approved, nothing to send, or the context engine could not answer), the surfaces' graph statuses and the decisions stored.</summary>
/// <param name="Outcome">'proposed' when something was sent, else 'skipped'.</param>
/// <param name="Reason">Why it skipped, or null.</param>
/// <param name="Surfaces">Where each proposed surface stands, in order.</param>
/// <param name="Decisions">How many decision records were stored.</param>
public sealed record ProposePlanContextResponse(string Outcome, string? Reason, IReadOnlyList<ScopeSurfaceStatus> Surfaces, int Decisions);
