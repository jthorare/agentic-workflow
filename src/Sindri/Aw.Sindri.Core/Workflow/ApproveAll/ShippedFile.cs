using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>One recipe of the embedded set, read (setup-once spec §5.3).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash of its normalized long form.</param>
/// <param name="Document">The bound recipe.</param>
/// <param name="File">The file's path relative to <c language="text">recipes/sindri</c>.</param>
/// <param name="Text">The file's text, as embedded.</param>
public sealed record ShippedFile(RecipeId Id, RecipeHash Hash, RecipeDocument Document, string File, string Text)
{
    /// <summary>The recipe.</summary>
    public Recipe Recipe => Document.Recipe;

    /// <summary>Its version.</summary>
    public int Version => Recipe.Version;

    /// <summary>The name shown instead of the id (the id itself when the recipe names none).</summary>
    public string Name => Recipe.DisplayName ?? Id.Value;
}
