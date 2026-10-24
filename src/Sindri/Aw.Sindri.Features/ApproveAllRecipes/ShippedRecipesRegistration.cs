using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.RegistryWrites;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>Registers the shipped recipes (setup-once spec §5.3) and the registry services they share with the single approval.</summary>
internal static class ShippedRecipesRegistration
{
    /// <summary>Adds <see cref="IShippedRecipes"/> over the embedded set, unless a host already supplied one.</summary>
    /// <param name="services">The collection.</param>
    /// <returns>The collection.</returns>
    internal static IServiceCollection AddShippedRecipes(this IServiceCollection services)
    {
        services.TryAddScoped<RecipeRegistrar>();
        services.TryAddScoped<RecipeApprovalRecorder>();
        services.TryAddScoped<ShippedRecipeSet>();
        services.TryAddScoped<ShippedPlanRows>();
        services.TryAddScoped<ShippedRecipePlanner>();
        services.TryAddScoped<ShippedRecipeApprover>();
        services.TryAddScoped<IShippedRecipes, ShippedRecipes>();
        return services;
    }
}
