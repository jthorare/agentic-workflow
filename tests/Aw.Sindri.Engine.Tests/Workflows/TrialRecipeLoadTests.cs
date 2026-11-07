using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Exceptions;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A skills-folder trial's 'LoadRecipe' lays the folder's block over serve's catalog and holds it to the pin its approval took (#2148 bug 23, #2219).</summary>
[Lane(TestLane.Unit)]
public sealed class TrialRecipeLoadTests
{
    private const string Review = "schema: 1\nblocks:\n  - id: review\n    sentence: Reviews a pull request.\n    inputs:  { pr: { type: text, label: Pull request (number or link), required: true } }\n    args: [\"{pr}\"]\n    outputs: { findings: { type: path, from: review_file } }\n    outcomes:\n      from: verdict\n      map: { PASS: pass }\n      success: pass\n    pushes: false\n    maxDuration: 2h\n";

    private static string Widened => Review.Replace("{ PASS: pass }", "{ PASS: pass, BLOCKED: blocked }", StringComparison.Ordinal);

    private static string Only => Review.Replace("id: review", "id: extra", StringComparison.Ordinal);

    [Fact]
    public async Task A_trial_whose_folder_block_maps_an_extra_outcome_loads_the_folders_descriptor()
    {
        using var folder = Folder("review", Widened);

        var loaded = await Trial("review", folder.Path, approvedFolder: folder.Path);

        var block = Assert.Single(loaded.Blocks);
        Assert.Contains("blocked", block.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_trial_of_a_block_only_the_folder_has_loads_it()
    {
        using var folder = Folder("extra", Only);

        var loaded = await Trial("extra", folder.Path, approvedFolder: folder.Path);

        Assert.Equal("step.skill.extra", Assert.Single(loaded.Blocks).Id.Value);
    }

    [Fact]
    public async Task A_folder_changed_since_the_trial_was_approved_is_refused_and_not_retried()
    {
        using var approved = Folder("review", Widened);
        using var changed = Folder("review", Review);

        var refused = await Assert.ThrowsAsync<ApplicationFailureException>(() => Trial("review", changed.Path, approvedFolder: approved.Path));

        Assert.True(refused.NonRetryable);
        Assert.Contains("step.skill.review", refused.Message, StringComparison.Ordinal);
        Assert.Contains("the skills folder changed since this trial started (fix: start the trial again)", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_plain_recipe_still_loads_serves_descriptors()
    {
        var facts = new InMemoryFactStore();
        var recipe = RecipeOf("workflow.review-it", "review");
        var version = new RecipeVersion(recipe.Id, 1, RegistryData.Hash("plain"));

        var loaded = await new ActivityEnvironment().RunAsync(() => Activities(recipe, version, facts, new InMemoryRecipeApprovals()).LoadRecipeAsync(version, folder: null));

        Assert.DoesNotContain("blocked", Assert.Single(loaded.Blocks).Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
    }

    // The trial of <block> given <folder>; its approval pinned the block as <approvedFolder> held it.
    private static async Task<LoadedRecipe> Trial(string block, string folder, string approvedFolder)
    {
        var facts = new InMemoryFactStore();
        var recipe = RecipeOf(SkillTrialRecipe.IdPrefix + block, block);
        var version = new RecipeVersion(recipe.Id, 1, RegistryData.Hash("trial-" + block));
        var pinned = SkillManifestLoader.Load(approvedFolder).Loaded.Single(b => b.Skill == block).Descriptor;
        var approvals = new InMemoryRecipeApprovals();
        _ = await approvals.Approve(new RecipeApproval(recipe.Id, 1, version.Hash, "human:cli", KnownInstants.Epoch, ApprovalVia.Desk, Base: null,
            new RecipePins([new BlockPin(pinned.Id, PinRules.Hash(pinned, SliceHarness.Json), PinRules.Interface(pinned, SliceHarness.Json))], [])), TestContext.Current.CancellationToken);
        var stored = await facts.Put(Encoding.UTF8.GetBytes(folder), TestContext.Current.CancellationToken);

        return await new ActivityEnvironment().RunAsync(() => Activities(recipe, version, facts, approvals).LoadRecipeAsync(version, stored.Value));
    }

    private static Recipe RecipeOf(string id, string block)
    {
        var recipe = ARecipe.Minimal(RecipeId.From(id));
        return recipe with { States = recipe.States.ToDictionary(static s => s.Key, s => s.Value.Step is { } step ? s.Value with { Step = step with { Use = BlockId.From("step.skill." + block) } } : s.Value, StringComparer.Ordinal) };
    }

    private static RunStepActivities Activities(Recipe recipe, RecipeVersion version, InMemoryFactStore facts, InMemoryRecipeApprovals approvals)
    {
        // serve's loaded set is the fixture folder: its review maps PASS and NEEDS_CHANGES
        var catalog = new BlockCatalog([.. SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Select(static b => b.Descriptor)], []);
        var profiles = new InMemoryProfiles();
        var runner = new BlockStepRunner(new BlockStepRuntimes([]), catalog, static (_, _, _) => throw new NotSupportedException(), facts, facts, profiles, new StepContexts(new MemoryInjectionTexts(), new Inputs.NoRunner()));
        return new RunStepActivities(new Approved(new ApprovedRecipe(version, recipe)), catalog, new ProfileWorkflowTimes(profiles, profiles), runner, new AgentStepSelection(profiles, []), new TrialBlocks([new FolderBlocks()], approvals, facts, SliceHarness.Json), NullLogger<RunStepActivities>.Instance);
    }

    private static TempDirectory Folder(string block, string yaml)
    {
        var folder = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(folder.Path, block));
        File.WriteAllText(Path.Combine(folder.Path, block, "block.yaml"), yaml);
        File.WriteAllText(Path.Combine(folder.Path, block, "SKILL.md"), File.ReadAllText(Path.Combine(SkillFixtures.Folder, "review", "SKILL.md")).Replace("\"skill\": \"review\"", $"\"skill\": \"{block}\"", StringComparison.Ordinal));
        return folder;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("trial-load").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class FolderBlocks : ISkillBlocks
    {
        public string Folder => string.Empty;

        public IReadOnlyList<LoadedSkillBlock> Loaded => [];

        public IReadOnlyList<SkippedSkillBlock> Skipped => [];

        public SkillBlocks LoadFolder(string folder) => SkillManifestLoader.Load(folder);
    }

    private sealed class Approved(ApprovedRecipe recipe) : IApprovedRecipes
    {
        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion version, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }
}
