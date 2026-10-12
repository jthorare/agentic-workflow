using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>recipe validate's service over real files in a temp directory (spec §9.1): the reader, the validator, the hash and the same-directory children.</summary>
public sealed class ValidateRecipeServiceTests : IDisposable
{
    private static readonly string _kid =
        "schema: 1\n"
        + "id: workflow.kid\n"
        + "version: 1\n"
        + "initial: produce\n"
        + "states:\n"
        + "  produce:\n"
        + "    kind: active\n"
        + "    step: step.produce\n"
        + "    timeout: { after: 4h, then: park }\n"
        + "    next: \"Produce the Step output.\"\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n"
        + "  - { from: produce, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }\n";

    private readonly RecipeDirectory _directory = new();
    private readonly IRecipeFiles _files = new FileRecipeFiles();

    public void Dispose() => _directory.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task A_valid_recipe_file_validates_to_its_hash()
    {
        var path = _directory.Write("workflow.minimal.yaml", RecipeReaderTests.Minimal);
        var expected = RecipeReaderTests.Reader.Read(RecipeReaderTests.Minimal, "x.yaml").Document!.Hash;

        var response = (await Service().Handle(ValidateRecipeRequest.FromPath(path), TestContext.Current.CancellationToken)).Value;

        Assert.True(response.Passed, string.Join('\n', response.Findings.Select(f => f.ToLine())));
        Assert.Equal(expected, response.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_finding_names_the_file_without_its_directory()
    {
        var path = _directory.Write("RCP-SCHEMA-001.yaml", RecipeFixtures.Read("invalid/RCP-SCHEMA-001.yaml"));

        var response = (await Service().Handle(ValidateRecipeRequest.FromPath(path), TestContext.Current.CancellationToken)).Value;

        Assert.False(response.Passed);
        Assert.Null(response.Hash);
        Assert.StartsWith("RCP-SCHEMA-001: 'retries' isn't a field of a state at RCP-SCHEMA-001.yaml:10:5 ", Assert.Single(response.Findings).ToLine(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_file_is_SND_RECIPE_010()
    {
        var result = await Service().Handle(ValidateRecipeRequest.FromPath(Path.Combine(_directory.Path, "none.yaml")), TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.RecipeUnreadable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_child_recipe_resolves_from_the_same_directory_with_its_hash()
    {
        var parent = _directory.Write("workflow.parent.yaml", RecipeReaderTests.Minimal);
        _ = _directory.Write("workflow.kid.yaml", _kid);
        var recipe = RecipeReaderTests.Reader.Read(RecipeReaderTests.Minimal, "p.yaml").Document!.Recipe;
        var waiting = recipe with
        {
            States = new Dictionary<string, Core.Workflow.Recipes.RecipeState>(recipe.States, StringComparer.Ordinal)
            {
                ["running"] = new(Core.Workflow.Recipes.StateKind.Wait, Step: null,
                    new Core.Workflow.Recipes.WaitFor(Event: null, Answer: null, Resource: null,
                        new Core.Workflow.Recipes.ChildWait(RecipeId.From("workflow.kid"), new Dictionary<FactKey, Core.Workflow.Recipes.WithValue>(), Core.Workflow.Recipes.OnParked.Propagate)),
                    Timeout: null, Loop: null, Budget: null, Next: null, Outcome: null),
            },
        };

        var children = await SameDirectoryChildren.Load(parent, waiting, RecipeReaderTests.Reader, _files, TestContext.Current.CancellationToken);

        Assert.Equal(RecipeReaderTests.Reader.Read(_kid, "k.yaml").Document!.Hash, children.Resolve(RecipeId.From("workflow.kid"))!.Hash);
        Assert.Null(children.Resolve(RecipeId.From("workflow.absent")));
    }

    private ValidateRecipeService Service() =>
        new(RecipeReaderTests.Reader, _files, new BlockCatalog([TestBlocks.Step("step.produce")], []));
}
