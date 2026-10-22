namespace Aw.Sindri.Features.Docs;

/// <summary>One finding: the rule, the doc it is about (null for the worktree), what is wrong and what to do.</summary>
/// <param name="Rule">The rule id.</param>
/// <param name="Item">The doc; null when it is about the worktree.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">What to do.</param>
internal sealed record DocsCheckFinding(string Rule, string? Item, string Message, string Fix);
