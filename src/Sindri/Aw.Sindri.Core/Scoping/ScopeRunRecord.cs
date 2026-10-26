namespace Aw.Sindri.Core.Scoping;

/// <summary>One agent run of a scoping instance as the ledger records it.</summary>
/// <param name="Step">The descriptor id of the state's Step ('step.scope.draft').</param>
/// <param name="State">The recipe state the run belonged to ('draft', 'challenge', 'relevance', 'gap-search').</param>
/// <param name="Round">The launch's run number.</param>
/// <param name="Via">'agent' for a launched session, 'desk' for a completion at the desk (which has no launch, so no model).</param>
/// <param name="Model">The model the session actually used (the launch's latest session.usage event), or null when none was recorded.</param>
/// <param name="Requested">The model the state's 'with.model' asked for, or null when it asked for none.</param>
/// <param name="Vendor">The model's vendor, or null when unknown.</param>
/// <param name="Tokens">The tokens the run used.</param>
/// <param name="Launch">The launch id, or null for a desk completion.</param>
/// <param name="RunToken">The Step run's token ('{entry}.{n}') the session passes as --run, or null when it can't be told; the context call log is keyed by it.</param>
public sealed record ScopeRunRecord(string Step, string State, int Round, string Via, string? Model, string? Requested, string? Vendor, long Tokens, string? Launch, string? RunToken = null);
