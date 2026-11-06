using Aw.Kernel.Services;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Sindri.Features.SetReviewLogin;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the PR review page (PR review page spec §6.1, §9): the Reviews list and the login save.</summary>
internal static class ReviewServiceCollectionExtensions
{
    /// <summary>Adds SetReviewLogin behind the command chain, and the approval it settles.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddReviewCommands(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<SetReviewLoginRequest>, AcceptAllValidator<SetReviewLoginRequest>>().AddCommand<SetReviewLoginRequest, SetReviewLoginResponse, SetReviewLoginService>()
            .AddScoped<LoginApproval>();
    }

    /// <summary>Adds ListReviews behind the query chain, its facts over Context and the summary reader.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddReviewQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListReviewsRequest>, AcceptAllValidator<ListReviewsRequest>>().AddQuery<ListReviewsRequest, ListReviewsResponse, ListReviewsService>()
            .AddScoped<IReviewFacts, ContextReviewFacts>()
            .AddScoped<IReviewLibraryState, ContextLibraryState>()
            .AddScoped<IReviewProjects, ProjectReviewNames>()
            .AddScoped<ReviewSummaryReader>();
    }
}
