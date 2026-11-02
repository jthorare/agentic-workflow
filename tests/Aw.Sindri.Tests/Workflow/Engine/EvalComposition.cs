using System.Collections.Concurrent;
using System.Text.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The build's composition for the eval tests: its recipe reader, catalog and JSON options.</summary>
internal static class EvalComposition
{
    private static readonly Lazy<ServiceProvider> _provider = new(static () => Composition.Compose(
        new ServiceCollection(), static name => name == "AW_STATE_DIR" ? Path.Combine(Path.GetTempPath(), "aw-sindri-evals-test") : null, new FakeTimeProvider())
        .BuildServiceProvider());

    // why: the shipped recipes never change inside a run and a parsed recipe is immutable, so each is read once however many tests ask for it.
    private static readonly ConcurrentDictionary<string, Core.Workflow.Recipes.Recipe> _recipes = new(StringComparer.Ordinal);

    /// <summary>The package's AwJson options.</summary>
    public static JsonSerializerOptions Options => _provider.Value.GetRequiredService<JsonSerializerOptions>();

    /// <summary>The build's catalog.</summary>
    public static BlockCatalog Catalog => _provider.Value.GetRequiredService<BlockCatalog>();

    /// <summary>The shipped recipe at <paramref name="path"/> (relative to recipes/), with its hash.</summary>
    public static Core.Workflow.Recipes.Recipe Recipe(string path) => _recipes.GetOrAdd(path, Read);

    private static Core.Workflow.Recipes.Recipe Read(string path)
    {
        var document = _provider.Value.GetRequiredService<IRecipeReader>().Read(ShippedRecipes.Read(path), Path.GetFileName(path)).Document;
        Assert.NotNull(document);
        return document.Recipe with { Hash = document.Hash };
    }

    /// <summary>'recipe eval' over the build's reader, catalog and options, reading recipes and evals from <paramref name="files"/>.</summary>
    public static EvalRecipeService Service(InMemoryRecipeFiles files) => new(
        _provider.Value.GetRequiredService<IRecipeReader>(), files, _provider.Value.GetRequiredService<BlockCatalog>(), Options);
}
