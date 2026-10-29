using System.Text.Json;
using Aw.Context.Core.Models;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.Models;

/// <summary>The one registration of the Jev label pick (model routing spec §11, R-5): the choice port over the judge's batch classifier, and the route's prompt budget the labeler sizes its packs by.</summary>
internal static class ChoicePortRegistration
{
    /// <summary>Registers <see cref="IChoicePort"/>, <see cref="IModelPromptBudget"/> and the 'context:labelpick' doctor row.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (the judge's state directory).</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddChoicePort(this IServiceCollection services, Func<string, string?> environment) => services
        .AddSingleton<IChoicePort>(sp =>
        {
            var judge = new JudgeCatalog(environment, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<JsonSerializerOptions>());
            return new JevChoicePort(judge.Classifier, judge.Providers, sp.GetRequiredService<IApprovedModels>(), sp.GetRequiredService<Aw.Sindri.Core.Jobs.IJobContextAccessor>(),
                sp.GetRequiredService<IModelCallLog>(), sp.GetRequiredService<Aw.Kernel.Ids.IIdGenerator>(), sp.GetRequiredService<TimeProvider>());
        })
        .AddSingleton<IModelPromptBudget>(sp => sp.GetRequiredService<IModelPort>() as IModelPromptBudget ?? new WidePromptBudget())
        .AddScoped<IDoctorChecks>(sp => new LabelPickDoctorChecks(sp.GetRequiredService<IApprovedModels>(), environment));   // the 'context:labelpick' row (spec §8.4)

    // A host whose model port is not the router has no harness list to size against: the wide budget is today's behavior (the generate packs are still bigger, within 60,000 characters).
    private sealed class WidePromptBudget : IModelPromptBudget
    {
        public ValueTask<PromptBudget?> For(ModelPurpose purpose, CancellationToken cancellationToken) => ValueTask.FromResult<PromptBudget?>(PromptBudget.Wide);
    }
}
