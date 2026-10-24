using Aw.Context.Core.Review;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The override log, newest first (GET /sindri/context/map/review/history).</summary>
/// <param name="Limit">The most entries (1 to 10000), or null for 200.</param>
public sealed record GetContextReviewHistoryRequest(int? Limit = null) : IQuery<ReviewHistoryView>;
