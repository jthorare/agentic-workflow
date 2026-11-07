using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>A skills-folder trial whose block maps fewer outcomes than serve's is pinned to the folder's block; the real approval check lets its commands through instead of refusing them with pin drift (#2148, bug 22).</summary>
[Lane(TestLane.Unit)]
public sealed class TrialCommandTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    [Fact(Timeout = 30_000)]
    public async Task A_narrowed_map_skills_folder_trials_complete_goes_through_the_command_path()
    {
        var served = Block(SkillFixtures.Review);   // PASS and NEEDS_CHANGES, the set serve loaded
        var folder = Block(SkillFixtures.Review with { Outcomes = SkillFixtures.Review.Outcomes with { Map = new Dictionary<string, string>(StringComparer.Ordinal) { ["PASS"] = "pass" } } });
        var plan = SkillTrialRecipe.Build(folder, ["pr"], foldered: true).Value;
        var document = new YamlRecipeReader(_json).Read(plan.Text, plan.Id.Value + ".yaml").Document!;
        var store = new InMemoryRecipeStore();
        var approvals = new InMemoryRecipeApprovals();
        var registered = (await store.Add(new RegisteredRecipe(plan.Id, plan.Version, document.Hash, Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null }, _json)), Base: null, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken)).Value.Recipe;
        _ = await approvals.Approve(RegistryData.Approval(registered, PinRules.Pins(document.Recipe, new BlockCatalog([folder.Descriptor], []), [], _json)), TestContext.Current.CancellationToken);   // pinned at the start, against the folder's block
        var recipes = new RegistryApprovedRecipes(store, approvals, new BlockCatalog([served.Descriptor], []), _json, new NoRenewals());
        var instance = new TemporalLikeRuntime { Recipe = new RecipeVersion(registered.Id, registered.Version, registered.Hash) };
        var service = new ApplyCommandService(instance, instance, recipes, new InMemoryCommandNonces(new FakeTimeProvider()), instance);

        var done = await service.Handle(new ApplyCommandRequest(instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Grant, Amount: 1, Nonce: CommandNonce.From(new string('c', 32))), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.ToString() : null);
        Assert.Equal(1, instance.Applied);
    }

    private static LoadedSkillBlock Block(SkillBlockManifest manifest) => new(manifest.Name, manifest, SkillDescriptors.Build(manifest).Value, "block.yaml", SkillDescriptors.HashOf(manifest));

    private sealed class NoRenewals : IPinRenewals
    {
        public ValueTask<Result<bool>> Renew(RecipeApproval standing, IReadOnlyList<BlockPin> build, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(false);
    }
}
