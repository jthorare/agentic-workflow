using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Reads recipe files for in-process validation (spec §9.1): the file 'recipe validate' names, and the child recipes
/// beside it (a 'child' recipe resolves from the same directory when Sindri isn't served).
/// </summary>
public interface IRecipeFiles
{
    /// <summary>The text of the recipe file at <paramref name="path"/>.</summary>
    /// <param name="path">The file, as the user gave it or as a sibling of one.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file's text, or SND-RECIPE-010 when it doesn't exist or can't be read.</returns>
    public ValueTask<Result<string>> Read(string path, CancellationToken cancellationToken);
}
