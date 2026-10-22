using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The subject the start names (spec section 3.11) and what the workflow.started row records of it.</summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>The producer of an input the start generated from its template.</summary>
    private const string GenerateActor = "engine:generate";

    /// <summary>
    /// The instance's subject (spec section 3.11): the first input names it, and the recipe's name, rendered once from the facts it
    /// reads at the start time, is its label. A recipe registered before input kinds is not named. Each fact is read by the 'ReadSubject' local activity; one that can't be read is its bare key.
    /// The stored text of the link inputs comes back too: a link fact's ref shows its URL, host and title from it.
    /// </summary>
    private static async Task<(SubjectRef? Subject, IReadOnlyDictionary<FactKey, string> Links)> NamedSubject(InterpretedRecipe recipe, InstanceStart start)
    {
        if (SubjectKinds.Legacy(recipe.Recipe) is not null)
        {
            return (null, await LegacyRun(recipe, start).ConfigureAwait(true));
        }

        var inputs = recipe.Recipe.Inputs;
        List<FactKey> keys = [.. InstanceNames.FactsRead(recipe.Recipe)];
        if (SubjectRefs.NeedsContent(recipe))
        {
            keys.Add(inputs[0].Key);
        }

        keys.AddRange(inputs.Where(static i => SubjectKinds.Effective(i) == "link").Select(static i => i.Key));
        var contents = new Dictionary<FactKey, string?>();
        var links = new Dictionary<FactKey, string>();
        foreach (var key in keys.Distinct())
        {
            if (inputs.Any(i => i.Key == key && i.Kind is not ("item" or "file")) && start.Inputs.TryGetValue(key, out var fact))
            {
                var text = await SubjectTextOf(fact.Value).ConfigureAwait(true);
                contents[key] = text;
                if (text is not null && inputs.Any(i => i.Key == key && SubjectKinds.Effective(i) == "link"))
                {
                    links[key] = text;
                }
            }
        }

        var name = InstanceNames.Render(recipe, start.Item, contents, start.StartedAt);
        return (SubjectRefs.Of(recipe, start.Item, contents.GetValueOrDefault(inputs[0].Key), name), links);
    }

    /// <summary>
    /// A pre-P4c instance replayed: no kind to name it by (the start refuses such a recipe now), so it runs unnamed. It still makes the one
    /// 'ReadSubject' call the pre-P4c start made, because a history recorded then holds that marker and a replay must match it.
    /// </summary>
    private static async Task<IReadOnlyDictionary<FactKey, string>> LegacyRun(InterpretedRecipe recipe, InstanceStart start)
    {
        if (SubjectRefs.NeedsContent(recipe) && start.Inputs.TryGetValue(recipe.Recipe.Inputs[0].Key, out var first))
        {
            _ = await SubjectTextOf(first.Value).ConfigureAwait(true);
        }

        return new Dictionary<FactKey, string>();
    }

    /// <summary>
    /// The workflow.started detail with what the ledger alone must show (spec, P4c amendment 'When name is rendered'): the subject with
    /// the rendered name as its label, each fact's producer ('engine:generate' for an input the start generated), and the recipe's id and hash (the recipe stats' key).
    /// </summary>
    private WorkflowEventDetail Named(WorkflowEventDetail detail, InstanceStart start) =>
        detail with
        {
            Delta = detail.Delta with
            {
                Subject = _subject is { } s ? new EventSubject(s.Kind, s.Key, s.Label.Length > 0 ? s.Label : start.WorkflowId().Value) : null,
                Recipe = start.Recipe.Id.Value,
                RecipeHash = start.Recipe.Hash.Value,
                Producers = detail.Delta.Facts?.Keys.ToDictionary(
                    static k => k,
                    k => start.Inputs.TryGetValue(FactKey.From(k), out var input) && input.Generated ? GenerateActor : detail.TriggerActor,
                    StringComparer.Ordinal),
            },
        };
}
