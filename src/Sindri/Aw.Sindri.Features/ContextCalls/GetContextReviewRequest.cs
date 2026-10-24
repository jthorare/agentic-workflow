using Aw.Context.Core.Review;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What is left to decide in the latest draft (GET /sindri/context/map/review).</summary>
/// <param name="Limit">The most entries per group (1 to 10000), or null for 100.</param>
public sealed record GetContextReviewRequest(int? Limit = null) : IQuery<ReviewQueueView>;
