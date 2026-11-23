namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One rule record as 'rule_catalog' lists it.</summary>
/// <param name="Id">The record's id, such as 'engineering.csharp.ado-net-row-mappers'.</param>
/// <param name="Kind">The record's kind, such as 'engineering'; empty when it has none.</param>
/// <param name="Section">The spec section the rule comes from, such as '4b.2'; empty when it has none.</param>
/// <param name="Text">The rule itself.</param>
/// <param name="Status">The record's status, such as 'active'; empty when it has none.</param>
/// <param name="Checks">The checks that enforce the rule.</param>
public sealed record RuleListing(string Id, string Kind, string Section, string Text, string Status, IReadOnlyList<RuleCheckListing> Checks);
