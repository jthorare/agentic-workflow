namespace Aw.Sindri.Core.Handoff;

/// <summary>One page of unclaimed offers.</summary>
/// <param name="Offers">The offers on this page, oldest first.</param>
/// <param name="NextPageToken">The token for the next page, or null on the last page.</param>
public sealed record OfferPage(IReadOnlyList<Offer> Offers, string? NextPageToken);
