using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>
/// The child recipes beside a recipe file (spec §9.1: not served, 'workflow.item.yaml' finds 'workflow.step.yaml'):
/// '&lt;child id&gt;.yaml' in the same directory, read before validation so the validator stays pure, and followed through
/// the children's own children. A child file that is missing or doesn't bind doesn't resolve (RCP-CHILD-001).
/// </summary>
internal sealed class SameDirectoryChildren : IChildRecipeResolver
{
    /// <summary>The most child files one validation reads; past the CHILD rules' depth of 4, this only bounds the work.</summary>
    internal const int MaxFiles = 64;

    private readonly Dictionary<RecipeId, ChildRecipe> _byId;

    private SameDirectoryChildren(Dictionary<RecipeId, ChildRecipe> byId)
    {
        _byId = byId;
    }

    /// <summary>No children resolve (a recipe given as text has no directory).</summary>
    public static SameDirectoryChildren None => new([]);

    /// <summary>Reads the children <paramref name="recipe"/> names, and theirs, from <paramref name="path"/>'s directory.</summary>
    public static async ValueTask<SameDirectoryChildren> Load(string path, Recipe recipe, IRecipeReader reader, IRecipeFiles files, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var found = new Dictionary<RecipeId, ChildRecipe>();
        var seen = new HashSet<RecipeId> { recipe.Id };
        var queue = new Queue<RecipeId>(ChildIds(recipe));
        while (queue.TryDequeue(out var id) && seen.Count <= MaxFiles)
        {
            if (!seen.Add(id))
            {
                continue;
            }

            var file = id.Value + ".yaml";
            var text = await files.Read(Path.Combine(directory, file), cancellationToken).ConfigureAwait(false);
            if (text.IsError || reader.Read(text.Value, file).Document is not { } child || !child.Recipe.Id.Equals(id))
            {
                continue;
            }

            found[id] = new ChildRecipe(child.Recipe, child.Hash);
            foreach (var next in ChildIds(child.Recipe))
            {
                queue.Enqueue(next);
            }
        }

        return new SameDirectoryChildren(found);
    }

    /// <inheritdoc/>
    public ChildRecipe? Resolve(RecipeId id) => _byId.GetValueOrDefault(id);

    private static IEnumerable<RecipeId> ChildIds(Recipe recipe) =>
        recipe.States.Values.Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>();
}
