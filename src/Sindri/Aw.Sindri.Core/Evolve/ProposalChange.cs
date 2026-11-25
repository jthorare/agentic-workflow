namespace Aw.Sindri.Core.Evolve;

/// <summary>The change a proposal asks for: the repo-relative files it touches and what to do to them.</summary>
/// <param name="Files">The files, as the model named them (normalized by <see cref="RepoPath"/> before any decision).</param>
/// <param name="Description">What to change.</param>
public sealed record ProposalChange(IReadOnlyList<string> Files, string Description);
