using Aw.Context.Core.Concepts;
using Aw.Context.Features.GetConcepts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Context.Features;

/// <summary>The kept concepts and glossary answers (issue 3676).</summary>
public static partial class ContextFeaturesServiceCollectionExtensions
{
    // The answers the concepts and glossary reads and the publish fill share; a host without Aw.Context.Infrastructure keeps none and builds every answer.
    private static IServiceCollection AddConceptAnswers(this IServiceCollection services)
    {
        services.TryAddSingleton<IConceptAnswerStore, UnkeptConceptAnswerStore>();   // the host that wires Aw.Context.Infrastructure replaces it with the SQLite store
        return services.AddScoped<ConceptAnswers>()
            .AddSingleton<IConceptAnswerFills>(static sp => new ConceptAnswerFills(async (scope, ct) =>
            {
                // the fill runs after the publish's own scope ended, so it reads in a scope of its own
                await using var own = sp.CreateAsyncScope();
                await own.ServiceProvider.GetRequiredService<ConceptAnswers>().Fill(scope, ct).ConfigureAwait(false);
            }));
    }
}
