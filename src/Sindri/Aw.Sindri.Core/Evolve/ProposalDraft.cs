namespace Aw.Sindri.Core.Evolve;

/// <summary>
/// Where a proposal's real edit lives: the local branch and commit the workflow's agent step left it on, such as 'wf/&lt;slug&gt;/&lt;run&gt;/work'.
/// A pointer only; the store never reads the branch.
/// </summary>
/// <param name="Branch">The local branch.</param>
/// <param name="Commit">The commit on it.</param>
public sealed record ProposalDraft(string Branch, string Commit);
