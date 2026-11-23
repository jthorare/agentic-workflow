using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.MarkWorkflowRunSeen;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the MarkWorkflowRunSeen command (E1): the owner opened a failed run, so its Home row clears.</summary>
internal static class MarkWorkflowRunSeenServiceCollectionExtensions
{
    /// <summary>Adds the command behind the command chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddMarkWorkflowRunSeen(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<MarkWorkflowRunSeenRequest>, AcceptAllValidator<MarkWorkflowRunSeenRequest>>().AddCommand<MarkWorkflowRunSeenRequest, MarkWorkflowRunSeenResponse, MarkWorkflowRunSeenService>();
    }
}
