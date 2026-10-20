using Aw.Kernel;

namespace Aw.Sindri.Features.Recipes.PromptEvals;

/// <summary>Reads a prompt eval's cases file (JSON) and says what is wrong with it by path (SND-RECIPE-012).</summary>
internal static class PromptCaseReader
{
    /// <summary>Reads <paramref name="text"/>.</summary>
    /// <param name="text">The cases file's text.</param>
    /// <returns>The file, or SND-RECIPE-012 naming the first problem.</returns>
    public static Result<PromptCaseFile> Read(string text) => throw new NotSupportedException();
}
