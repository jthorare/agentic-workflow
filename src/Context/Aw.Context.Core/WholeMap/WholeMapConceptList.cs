namespace Aw.Context.Core.WholeMap;

/// <summary>One page of an opened concept's member list (#3674): where the page is, and how many items each tab holds, so the card's count and the list's agree.</summary>
/// <param name="Tab">The tab this page is of: 'all', 'code', 'feature' or 'record'.</param>
/// <param name="Page">The page this answer holds (1-based).</param>
/// <param name="Pages">How many pages the tab has.</param>
/// <param name="Items">How many items the tab holds in all.</param>
/// <param name="Code">How many code files the concept holds (the code its features are implemented by).</param>
/// <param name="Features">How many features the concept holds.</param>
/// <param name="Records">How many records and decisions the concept holds.</param>
public sealed record WholeMapConceptList(string Tab, int Page, int Pages, int Items, int Code, int Features, int Records);
