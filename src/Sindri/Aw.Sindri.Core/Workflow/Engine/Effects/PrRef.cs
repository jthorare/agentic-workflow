namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>A pull request on a forge (PR babysitter spec §5.4: the first three columns of 'pr_comment_marks').</summary>
/// <param name="Forge">The forge ('github', 'file').</param>
/// <param name="Repo">The repository ('owner/name').</param>
/// <param name="Number">The pull request number.</param>
public sealed record PrRef(string Forge, string Repo, int Number);
