namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One page of an opened concept's member list (#3674).</summary>
/// <param name="Tab">The tab this page is of: all, code, feature or record.</param>
/// <param name="Page">The page this answer holds (1-based).</param>
/// <param name="Pages">How many pages the tab has.</param>
/// <param name="Items">How many items the tab holds in all.</param>
/// <param name="Code">How many code files the concept holds.</param>
/// <param name="Features">How many features the concept holds.</param>
/// <param name="Records">How many records and decisions the concept holds.</param>
internal sealed record WholeConceptList(string Tab, int Page, int Pages, int Items, int Code, int Features, int Records);
