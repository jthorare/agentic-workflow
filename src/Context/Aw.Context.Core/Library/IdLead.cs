namespace Aw.Context.Core.Library;

/// <summary>What the start of a heading, list item or table cell says about an item id.</summary>
/// <param name="Shape">Whether it is not an id, a good one or a bad one.</param>
/// <param name="Kind">The id's kind when good.</param>
/// <param name="Key">The id when good (a phase without leading zeros); the text read when bad.</param>
internal readonly record struct IdLead(IdShape Shape, DesignItemKind Kind, string Key);
