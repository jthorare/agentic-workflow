using System.Collections.Concurrent;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>The shipped recipes under recipes/sindri, read once, for the tests that walk the line's recipe tree.</summary>
internal static class LineTree
{
    private const string Directory = "sindri";

    private static readonly ConcurrentDictionary<string, RecipeDocument> _documents = new(StringComparer.Ordinal);
    private static readonly Lazy<InMemoryChildRecipes> _siblings = new(BuildSiblings);

    /// <summary>The recipe at 'sindri/&lt;id&gt;.yaml'.</summary>
    /// <param name="id">For example 'workflow.phase'.</param>
    /// <returns>The bound recipe.</returns>
    public static Recipe Recipe(string id) => Document(id).Recipe;

    /// <summary>The bound document at 'sindri/&lt;id&gt;.yaml'.</summary>
    /// <param name="id">For example 'workflow.phase'.</param>
    /// <returns>The document.</returns>
    public static RecipeDocument Document(string id) =>
        _documents.GetOrAdd(id, static key => RecipeComposition.ReadClean(ShippedRecipes.Read($"{Directory}/{key}.yaml"), $"{key}.yaml"));

    /// <summary>Every shipped sibling of the recipes in recipes/sindri, as a child resolver.</summary>
    /// <returns>The resolver.</returns>
    public static InMemoryChildRecipes Siblings() => _siblings.Value;

    private static InMemoryChildRecipes BuildSiblings()
    {
        var children = InMemoryChildRecipes.None;
        foreach (var path in ShippedRecipes.Recipes.Where(r => r.StartsWith(Directory + "/", StringComparison.Ordinal)))
        {
            var reading = RecipeComposition.Read(ShippedRecipes.Read(path), Path.GetFileName(path));
            if (reading.Document is { } document)
            {
                _ = children.With(new ChildRecipe(document.Recipe, document.Hash));
            }
        }

        return children;
    }

    /// <summary>The error lines validating <paramref name="document"/> against the shipped catalog and the shipped siblings reports.</summary>
    /// <param name="document">A bound recipe.</param>
    /// <returns>The errors.</returns>
    public static IReadOnlyList<string> Errors(RecipeDocument document) =>
        RecipeComposition.Errors(document, FixtureCatalog.Shipped(), Siblings());

    /// <summary>The recipes <paramref name="id"/> waits on as children, directly.</summary>
    /// <param name="id">A recipe id.</param>
    /// <returns>Their ids, in state order.</returns>
    public static IReadOnlyList<string> Children(string id) =>
        [.. Recipe(id).States.Values.Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>().Select(static c => c.Value).Distinct(StringComparer.Ordinal)];
}
