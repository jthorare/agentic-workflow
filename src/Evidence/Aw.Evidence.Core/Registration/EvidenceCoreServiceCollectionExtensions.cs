using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Visual;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Core.Registration;

/// <summary>Registers Aw.Evidence.Core's types.</summary>
public static class EvidenceCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Evidence domain services: the step runner and the visual gate, singletons (stateless, over the judge,
    /// run files, image codec, clock and JSON options, all singletons), and the data runs' file reader and writer, scoped like the slices that use it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidenceCore(this IServiceCollection services) =>
        services.AddSingleton<StepRunner>().AddSingleton<VisualGate>().AddScoped<DataRunFiles>();
}
