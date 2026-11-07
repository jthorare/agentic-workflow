namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>What Sindri holds on one issue: the item working it and that item's pull request.</summary>
/// <param name="Link">The item, or null.</param>
/// <param name="Pr">The item's pull request, or null.</param>
internal sealed record LinearIssueLinks(LinearItemLinkView? Link, LinearPrView? Pr);
