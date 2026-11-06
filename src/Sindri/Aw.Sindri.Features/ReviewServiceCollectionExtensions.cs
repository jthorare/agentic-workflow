using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Reviews list (PR review page spec §6.1).</summary>
internal static class ReviewServiceCollectionExtensions
{
    /// <summary>Adds ListReviews behind the query chain, its facts over Context and the summary reader.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddReviewQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListReviewsRequest>, AcceptAllValidator<ListReviewsRequest>>().AddQuery<ListReviewsRequest, ListReviewsResponse, ListReviewsService>()
            .AddScoped<IReviewFacts, ContextReviewFacts>()
            .AddScoped<IReviewProjects, ProjectReviewNames>()
            .AddScoped<ReviewSummaryReader>();
    }
}
