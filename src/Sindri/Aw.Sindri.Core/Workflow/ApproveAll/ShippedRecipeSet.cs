using System.Text;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The recipes this build ships (setup-once spec §5.3): the embedded set's flat <c language="text">*.yaml</c> files that bind, one per recipe id, in the set's order. A file that does not
/// bind is left out; <c language="text">recipe eval --shipped</c> and the registration report it. Read on every call, so a set a test swaps is seen at once.
/// </summary>
/// <param name="embedded">The embedded recipe set.</param>
/// <param name="reader">Reads a recipe's text.</param>
public sealed class ShippedRecipeSet(IEmbeddedRecipes embedded, IRecipeReader reader)
{
    /// <summary>The shipped recipes.</summary>
    /// <returns>One entry per recipe id.</returns>
    public IReadOnlyList<ShippedFile> Files()
    {
        List<ShippedFile> found = [];
        foreach (var file in embedded.Files().Where(static f => !f.RelativePath.Contains('/', StringComparison.Ordinal) && f.RelativePath.EndsWith(".yaml", StringComparison.Ordinal)))
        {
            var text = Encoding.UTF8.GetString(file.Bytes.Span);
            if (reader.Read(text, file.RelativePath).Document is { } document && !found.Exists(s => s.Id == document.Recipe.Id))
            {
                found.Add(new ShippedFile(document.Recipe.Id, document.Hash, document, file.RelativePath, text));
            }
        }

        return found;
    }
}
