using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>One rule record a <see cref="RuleCatalogPortContract"/> test seeds.</summary>
/// <param name="Id">The record's id.</param>
/// <param name="Section">Its spec section.</param>
/// <param name="Text">Its rule text.</param>
/// <param name="Checks">Its checks.</param>
public sealed record RuleCatalogSeed(string Id, string Section, string Text, IReadOnlyList<RuleCheckListing> Checks);
