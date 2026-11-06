using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.SetReviewLogin;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the PR review page's writes (PR review page spec §6.1, §9): the login save.</summary>
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
}
