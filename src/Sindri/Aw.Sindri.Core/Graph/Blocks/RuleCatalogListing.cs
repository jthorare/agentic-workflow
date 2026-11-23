namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What 'rule_catalog' reads: the rules and how many record files were skipped.</summary>
/// <param name="Rules">The rules, sorted by id.</param>
/// <param name="Skipped">How many record files could not be parsed or had no id or text.</param>
public sealed record RuleCatalogListing(IReadOnlyList<RuleListing> Rules, int Skipped);
