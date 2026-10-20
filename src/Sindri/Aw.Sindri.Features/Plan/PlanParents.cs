namespace Aw.Sindri.Features.Plan;

/// <summary>The hashes a plan packet's index.json lists as parents (line spec §3.2): the design first, then the scope.</summary>
/// <param name="Design">The design packet's hash.</param>
/// <param name="Scope">The scope packet's hash.</param>
public sealed record PlanParents(string Design, string Scope);
