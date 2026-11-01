using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.ListInstances;
using Aw.Sindri.Tests.Requests;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The label of the owner-stop row (#1862): the wait state's display name with only its first letter lower-cased, unless the first word is an acronym.</summary>
[Lane(TestLane.Unit)]
public sealed class OwnerStopLabelTests
{
    private const string Pull = """{"pr":42,"url":"https://github.com/o/r/pull/42"}""";

    private readonly RequestQueryHarness _q = new();
    private readonly InMemoryRecipeStore _recipes = new();
    private readonly ItemId _item = RequestQueryHarness.Item('1');

    [Theory]
    [InlineData("Ready to merge", "ready to merge")]
    [InlineData("Review PR", "review PR")]
    [InlineData("PR ready to merge", "PR ready to merge")]
    [InlineData(null, "waiting on you")]
    public async Task The_label_lower_cases_only_the_first_letter_and_keeps_an_acronym(string? displayName, string expected)
    {
        var view = await Waiting(displayName);

        var row = Assert.Single(await Read(view));

        Expect.Equal(expected, row.Label);
    }

    private async Task<InstanceView> Waiting(string? displayName)
    {
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml"), "workflow.pr-babysitter.yaml").Document!;   // why: the shipped recipes bind (ShippedRecipesTests)
        var states = document.Recipe.States.ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal);
        states["ready"] = states["ready"] with { DisplayName = displayName };
        var text = Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null, States = states }, SliceHarness.Json));
        _ = await _recipes.Add(new RegisteredRecipe(document.Recipe.Id, document.Recipe.Version, document.Hash, text, Base: null, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);
        await _q.CreateItem(_item, repo: null);
        var basis = StatusCases.View("desk-task");
        return basis with
        {
            Recipe = new RecipeVersion(document.Recipe.Id, document.Recipe.Version, document.Hash),
            State = "ready",
            Task = null,
            Run = null,
            AgentRun = null,
            Question = null,
            ItemId = ItemKey.From(_item.Value),
            FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prStatus"] = new string('1', 64) },
        };
    }

    private async Task<IReadOnlyList<NeedsYouRow>> Read(InstanceView view)
    {
        var instances = new SomeInstances([view]);
        var reader = new OwnerStopReader(
            new RecordingInstances([new InstanceRow(view.Address.Value, view.State, NeedsYou: false)]), instances, instances,
            new RecipeRegistryReader(_recipes, new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()), EngineFixture.Catalog);
        return await new NeedsYouOwnerStops(reader, _q.Views.Views, new Contents(), new PassThroughScrubber(), TimeProvider.System).Read(TestContext.Current.CancellationToken);
    }

    private sealed class Contents : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(Pull)));
    }

    private sealed class NoProfileFacts : IRecipeProfileFacts
    {
        public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException("reading a recipe back needs no profile");
    }
}
