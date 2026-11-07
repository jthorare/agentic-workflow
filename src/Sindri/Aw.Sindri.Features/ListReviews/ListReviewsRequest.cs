using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>The Reviews list (GET /sindri/reviews; PR review page spec §6.1).</summary>
public sealed record ListReviewsRequest() : IQuery<ListReviewsResponse>;
