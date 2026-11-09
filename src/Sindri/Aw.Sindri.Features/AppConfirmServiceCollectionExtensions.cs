using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.IssueChallenge;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the app's confirmation (setup-once spec §4): the challenge the app asks for.</summary>
internal static class AppConfirmServiceCollectionExtensions
{
    /// <summary>Adds IssueAppChallenge, a desk command behind the chain (the human token only; a session is refused before the handler).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddAppConfirm(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<IssueAppChallengeRequest>, AcceptAllValidator<IssueAppChallengeRequest>>()
            .AddCommand<IssueAppChallengeRequest, IssueChallengeResponse, IssueAppChallengeService>();
    }
}
