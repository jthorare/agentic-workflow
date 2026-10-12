using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The build's recipe reader, resolved from the real composition, and validation against a given catalog.</summary>
internal static class RecipeComposition
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-recipes-test");

    /// <summary>Reads <paramref name="text"/> with the build's IRecipeReader.</summary>
    /// <param name="text">The recipe's text.</param>
    /// <param name="file">Its file name.</param>
    /// <returns>What the reader made of it.</returns>
    public static RecipeReading Read(string text, string file)
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();
        return provider.GetRequiredService<IRecipeReader>().Read(text, file);
    }

    /// <summary>Reads <paramref name="text"/> and asserts it bound with no findings.</summary>
    /// <param name="text">The recipe's text.</param>
    /// <param name="file">Its file name.</param>
    /// <returns>The bound document.</returns>
    public static RecipeDocument ReadClean(string text, string file)
    {
        var reading = Read(text, file);
        Assert.Empty(reading.Findings.Select(f => f.ToLine()));
        return Assert.IsType<RecipeDocument>(reading.Document);
    }

    /// <summary>The error lines validating <paramref name="document"/> against <paramref name="catalog"/> reports.</summary>
    /// <param name="document">A bound recipe.</param>
    /// <param name="catalog">The blocks and events to validate against.</param>
    /// <param name="children">The child recipes it may start.</param>
    /// <returns>Every error finding's line.</returns>
    public static IReadOnlyList<string> Errors(RecipeDocument document, BlockCatalog catalog, IChildRecipeResolver children) =>
        [.. RecipeValidator.Validate(document, new ValidationContext(catalog, RecipeProfileFacts.None, children, BaseRecipe: null))
            .Where(f => f.Severity == FindingSeverity.Error)
            .Select(f => f.ToLine())];
}
