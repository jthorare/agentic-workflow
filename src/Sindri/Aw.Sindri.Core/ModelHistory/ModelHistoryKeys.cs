using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>The row keys of the 'model-history' view and of the 'model-runs' view the fold keeps beside it.</summary>
public static class ModelHistoryKeys
{
    /// <summary>The key of a 'model-history' row.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="version">Its version.</param>
    /// <param name="state">The state.</param>
    /// <param name="model">The model id.</param>
    /// <returns>'&lt;recipe&gt;@&lt;version&gt;|&lt;state&gt;|&lt;model&gt;'.</returns>
    public static string For(RecipeId recipe, int version, string state, string model) =>
        string.Create(CultureInfo.InvariantCulture, $"{recipe.Value}@{version}|{state}|{model}");

    internal static string Recipe(string hash) => "recipe/" + hash;

    internal static string Instance(string instance) => "instance/" + instance;

    internal static string Run(string instance, string state, int attempt) => string.Create(CultureInfo.InvariantCulture, $"run/{instance}/{state}/{attempt}");

    internal static string Order(string instance) => "order/" + instance;

    internal static string Completed(string instance, string state) => "completed/" + instance + "/" + state;
}
