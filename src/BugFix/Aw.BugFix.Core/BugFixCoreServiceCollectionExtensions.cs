using Aw.BugFix.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Core;

/// <summary>Registers Aw.BugFix.Core's services.</summary>
public static class BugFixCoreServiceCollectionExtensions
{
    /// <summary>Adds the Core services the slices share.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBugFixCore(this IServiceCollection services) =>
        services.AddScoped<CheckLocator>().AddScoped<WorkTree>().AddScoped<CheckPin>();
}
