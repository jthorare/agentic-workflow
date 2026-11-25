namespace Aw.Sindri.Core.Evolve;

/// <summary>A proposed change to the toolkit (issue 3787, piece 3). The original stays local; only a gated rewrite of it is ever public.</summary>
/// <param name="Kind">What it changes.</param>
/// <param name="Target">What it changes, by name: 'skill:&lt;name&gt;', 'rule:&lt;id&gt;' or 'doc:&lt;path&gt;'.</param>
/// <param name="Title">One line.</param>
/// <param name="Rationale">Why, in general words.</param>
/// <param name="Change">The files and the change.</param>
/// <param name="Evidence">Opaque refs of the turns behind it, never their text.</param>
public sealed record Proposal(ProposalKind Kind, string Target, string Title, string Rationale, ProposalChange Change, IReadOnlyList<string> Evidence);
