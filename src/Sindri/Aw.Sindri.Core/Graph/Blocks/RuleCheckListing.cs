namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One check of a rule record, as 'rule_catalog' lists it.</summary>
/// <param name="Type">The check's type, such as 'test' or 'review'.</param>
/// <param name="Id">The check's id.</param>
public sealed record RuleCheckListing(string Type, string Id);
