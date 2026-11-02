using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The states of the recipes this build embeds and of the recipes that are approved (model-pick spec §8): what a 'workflow.agent.pin' key of the form 'agent/stateId'
/// is checked against. An embedded recipe that doesn't read contributes nothing (its own check reports it).
/// </summary>
/// <param name="embedded">The embedded recipe set.</param>
/// <param name="reader">Reads a recipe's text.</param>
/// <param name="registry">The registered recipes and where each stands.</param>
internal sealed class RecipeStateCatalog(IEmbeddedRecipes embedded, IRecipeReader reader, RecipeRegistryReader registry) : IRecipeStateCatalog
{
    public async ValueTask<Result<IReadOnlyList<RecipeStateRef>>> All(CancellationToken cancellationToken)
    {
        var found = new HashSet<RecipeStateRef>();
        foreach (var file in embedded.Files().Where(static f => !f.RelativePath.Contains('/', StringComparison.Ordinal) && f.RelativePath.EndsWith(".yaml", StringComparison.Ordinal)))
        {
            if (reader.Read(Encoding.UTF8.GetString(file.Bytes.Span), file.RelativePath).Document is { } document)
            {
                found.UnionWith(document.Recipe.States.Keys.Select(state => new RecipeStateRef(document.Recipe.Id.Value, state)));
            }
        }

        var summaries = await registry.Summaries(cancellationToken).ConfigureAwait(false);
        if (summaries.IsError)
        {
            return summaries.Error;
        }

        foreach (var summary in summaries.Value.Where(static s => s.Standing == RecipeStanding.Approved))
        {
            found.UnionWith(summary.States.Select(place => new RecipeStateRef(summary.Id.Value, place.State)));
        }

        return Results.Success<IReadOnlyList<RecipeStateRef>>([.. found.OrderBy(static r => r.Recipe, StringComparer.Ordinal).ThenBy(static r => r.State, StringComparer.Ordinal)]);
    }
}
