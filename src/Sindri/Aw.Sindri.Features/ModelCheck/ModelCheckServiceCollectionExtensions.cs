using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>Registers the model check's block runtime (model-check design §3); its descriptors are registered with the blocks.</summary>
internal static class ModelCheckServiceCollectionExtensions
{
    /// <summary>Adds 'step.model.check''s runtime by block id, and doctor's double-run warning.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddModelCheckSlices(this IServiceCollection services) => services.AddBlockRuntime<ModelCheckStep>().AddScoped<IDoctorChecks, ModelCheckDoubleRunDoctor>();
}
