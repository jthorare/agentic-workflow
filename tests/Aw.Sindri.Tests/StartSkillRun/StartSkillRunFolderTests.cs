using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;
using Aw.Sindri.Features.StartSkillRun;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Sandbox.Egress;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.StartSkillRun;

/// <summary>'sindri skill run --skills &lt;dir&gt;' registers and approves its trial recipe against the folder's block, not serve's loaded one, and leaves what the loaded block is pinned to alone (#2148, bug 21).</summary>
[Lane(TestLane.Unit)]
public sealed class StartSkillRunFolderTests
{
    private const string Review = "schema: 1\nblocks:\n  - id: review\n    sentence: Reviews a pull request.\n    inputs:  { pr: { type: text, label: Pull request (number or link), required: true } }\n    args: [\"{pr}\"]\n    outputs: { findings: { type: path, from: review_file } }\n    outcomes:\n      from: verdict\n      map: { PASS: pass }\n      success: pass\n    pushes: false\n    maxDuration: 2h\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Widened => Review.Replace("{ PASS: pass }", "{ PASS: pass, NEEDS_CHANGES: needsChanges, BLOCKED: blocked }", StringComparison.Ordinal);

    private static string Narrowed => Review;

    private static string Only => Review.Replace("id: review", "id: extra", StringComparison.Ordinal);

    private static string Broken => Review.Replace("args: [\"{pr}\"]", "args: [\"{missing}\"]", StringComparison.Ordinal);

    [Fact]
    public async Task A_folder_block_that_maps_fewer_outcomes_registers_and_starts()
    {
        using var folder = Folder("review", Narrowed);
        await using var r = await Started();

        var run = await Run(r, "review", folder.Path);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
        Assert.NotNull(run.Runtime.Started);
    }

    [Fact]
    public async Task A_folder_block_that_maps_an_extra_outcome_registers_and_starts()
    {
        using var folder = Folder("review", Widened);
        await using var r = await Started();

        var run = await Run(r, "review", folder.Path);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
        Assert.NotNull(run.Runtime.Started);
    }

    [Fact]
    public async Task A_block_only_the_folder_has_registers_and_starts()
    {
        using var folder = Folder("extra", Only);
        await using var r = await Started();

        var run = await Run(r, "extra", folder.Path);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
        Assert.NotNull(run.Runtime.Started);
    }

    [Fact]
    public async Task A_trial_with_a_changed_map_leaves_the_loaded_blocks_run_pinned_as_it_was()
    {
        using var folder = Folder("review", Widened);
        await using var r = await Started();
        var plain = await Run(r, "review", skills: null);
        var before = await Pinned(r, plain.Runtime.Started!.Recipe);

        var trial = await Run(r, "review", folder.Path);
        var again = await Run(r, "review", skills: null);

        Assert.True(plain.Result.IsSuccess, Why(plain.Result));
        Assert.True(trial.Result.IsSuccess, Why(trial.Result));
        Assert.True(again.Result.IsSuccess, Why(again.Result));
        Assert.Equal(plain.Runtime.Started!.Recipe, again.Runtime.Started!.Recipe);   // the same version and hash: nothing was registered or approved for it again
        var after = await Pinned(r, again.Runtime.Started!.Recipe);
        Assert.Equal(before.Pins, after.Pins);
        Assert.Equal(before.ApprovedAt, after.ApprovedAt);
        Assert.Equal(LoadedHash(r, "review"), Assert.Single(after.Pins.Blocks).Hash);
        var folderPin = Assert.Single((await Pinned(r, trial.Runtime.Started!.Recipe)).Pins.Blocks);
        Assert.NotEqual(Assert.Single(after.Pins.Blocks).Hash, folderPin.Hash);   // the trial's own approval pinned the folder's block
        Assert.Equal(ApprovalCheck.Holds, (await r.Check(await Registered(r, again.Runtime.Started!.Recipe))).Value);
    }

    [Fact]
    public async Task A_folder_block_that_fails_its_manifest_refuses_with_the_manifests_reason()
    {
        using var folder = Folder("review", Broken);
        await using var r = await Started();

        var run = await Run(r, "review", folder.Path);

        Assert.True(run.Result.IsError);
        Assert.Contains("missing", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.NotEqual(WorkflowErrors.RecipeInvalid.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Null(run.Runtime.Started);
    }

    [Fact]
    public async Task A_narrowed_map_trial_still_holds_when_the_running_approval_is_checked()
    {
        using var folder = Folder("review", Narrowed);
        await using var r = await Started();

        var trial = await Run(r, "review", folder.Path);

        Assert.True(trial.Result.IsSuccess, Why(trial.Result));
        Assert.Equal(ApprovalCheck.Holds, (await r.Check(await Registered(r, trial.Runtime.Started!.Recipe))).Value);
    }

    [Fact]
    public async Task A_folder_only_block_trial_still_holds_when_the_running_approval_is_checked()
    {
        using var folder = Folder("extra", Only);
        await using var r = await Started();

        var trial = await Run(r, "extra", folder.Path);

        Assert.True(trial.Result.IsSuccess, Why(trial.Result));
        Assert.Equal(ApprovalCheck.Holds, (await r.Check(await Registered(r, trial.Runtime.Started!.Recipe))).Value);
    }

    [Fact]
    public async Task A_trial_whose_block_only_moved_its_hash_is_not_renewed_to_serves_block()
    {
        using var folder = Folder("review", (await File.ReadAllTextAsync(Path.Combine(SkillFixtures.Folder, "review", "block.yaml"), Token)).Replace("args: [\"{pr}\"]", "args: [\"--strict\", \"{pr}\"]", StringComparison.Ordinal));
        await using var r = await Started();
        var trial = await Run(r, "review", folder.Path);
        var version = trial.Runtime.Started!.Recipe;
        var before = await Pinned(r, version);

        var check = await r.Check(await Registered(r, version));

        Assert.True(trial.Result.IsSuccess, Why(trial.Result));
        Assert.NotEqual(LoadedHash(r, "review"), Assert.Single(before.Pins.Blocks).Hash);   // the fixture's hash moved: the folder's args differ, its interface does not
        Assert.Equal(ApprovalCheck.Holds, check.Value);
        var after = await Pinned(r, version);
        Assert.Equal(before.Pins, after.Pins);
        Assert.Equal(before.ApprovedAt, after.ApprovedAt);
        Assert.DoesNotContain(r.Events("recipe.approved"), static e => e.Envelope.Actor.EndsWith("pin-drift", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_revoked_folder_trial_reads_revoked()
    {
        using var folder = Folder("review", Narrowed);
        await using var r = await Started();
        var trial = await Run(r, "review", folder.Path);
        var recipe = await Registered(r, trial.Runtime.Started!.Recipe);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(recipe), Token);

        Assert.Equal(ApprovalCheck.Revoked, (await r.Check(recipe)).Value);
    }

    [Fact]
    public async Task A_plain_recipe_whose_block_only_moved_its_hash_is_still_renewed()
    {
        await using var r = await Started();
        var (recipe, pin) = await PlainWithPin(r);
        _ = await r.Approvals.Approve(RegistryData.Approval(recipe, new RecipePins([pin with { Hash = DescriptorHash.From("sha256:" + new string('a', 64)) }], [])), Token);

        var check = await r.Check(recipe);

        Assert.Equal(ApprovalCheck.Holds, check.Value);
        Assert.Equal(pin, Assert.Single((await r.Approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value!.Pins.Blocks));
    }

    [Fact]
    public async Task A_plain_recipe_whose_block_interface_changed_is_still_drifted()
    {
        await using var r = await Started();
        var (recipe, pin) = await PlainWithPin(r);
        _ = await r.Approvals.Approve(RegistryData.Approval(recipe, new RecipePins([pin with { Hash = DescriptorHash.From("sha256:" + new string('a', 64)), Interface = DescriptorHash.From("sha256:" + new string('b', 64)) }], [])), Token);

        Assert.Equal(ApprovalCheck.Drifted, (await r.Check(recipe)).Value);
    }

    // A recipe that is not a trial and names the loaded 'review' block (the trial's text under another id), with the pin this build makes for it.
    private static async Task<(RegisteredRecipe Recipe, BlockPin Pin)> PlainWithPin(RegistryHarness r)
    {
        var loaded = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(static b => string.Equals(b.Manifest.Name, "review", StringComparison.Ordinal));
        var text = SkillTrialRecipe.Build(loaded, ["pr"]).Value.Text.Replace("workflow.skill-trial-review", "workflow.review-plain", StringComparison.Ordinal);
        var recipe = await r.Register(RecipeReaderTests.Reader.Read(text, "plain.yaml").Document!);
        return (recipe, Assert.Single(PinRules.Blocks(RegistryHarness.Parse(recipe), r.Services.GetRequiredService<BlockCatalog>(), SliceHarness.Json)));
    }

    private static string Why(Result<StartSkillRunResponse> result) => result.IsError ? result.Error.ToString() : string.Empty;

    private static TempDirectory Folder(string block, string yaml)
    {
        var folder = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(folder.Path, block));
        File.WriteAllText(Path.Combine(folder.Path, block, "block.yaml"), yaml);
        File.WriteAllText(Path.Combine(folder.Path, block, "SKILL.md"), File.ReadAllText(Path.Combine(SkillFixtures.Folder, "review", "SKILL.md")).Replace("\"skill\": \"review\"", $"\"skill\": \"{block}\"", StringComparison.Ordinal));   // why: only the skill name; review_file stays what the output reads
        return folder;
    }

    // Serve's loaded set is the fixture folder (review maps PASS and NEEDS_CHANGES); its descriptors are in the catalog the registry validates against.
    private static async Task<RegistryHarness> Started()
    {
        var r = new RegistryHarness(static s =>
        {
            foreach (var block in SkillManifestLoader.Load(SkillFixtures.Folder).Loaded)
            {
                _ = s.AddBlockDescriptor(block.Descriptor);
            }

            _ = s.AddScoped<RecipeRegistrar>().AddScoped<RecipeApprovalRecorder>().AddScoped<TrialRecipes>();
        });
        _ = await r.H.HoldLease();
        return r;
    }

    private static DescriptorHash LoadedHash(RegistryHarness r, string block) =>
        PinRules.Hash(r.Services.GetRequiredService<BlockCatalog>().Find(BlockId.From("step.skill." + block))!, SliceHarness.Json);

    private static async Task<(Result<StartSkillRunResponse> Result, Capturing Runtime)> Run(RegistryHarness r, string block, string? skills)
    {
        var runtime = new Capturing();
        using var entered = r.H.Callers.Enter(RegistryHarness.Human);
        await using var scope = r.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var service = new StartSkillRunService(new SkillBlockRegistry(SkillFixtures.Folder), new OneItem(), sp.GetRequiredService<TrialRecipes>(), sp.GetRequiredService<InMemoryFactStore>(), runtime, TimeProvider.System, r.H.Callers);
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" };
        return (await service.Handle(new StartSkillRunRequest(block, "T-1", inputs, skills), Token), runtime);
    }

    private static async Task<RegisteredRecipe> Registered(RegistryHarness r, RecipeVersion version) =>
        (await r.Recipes.Find(version.Hash, Token)).Value!;

    private static async Task<RecipeApproval> Pinned(RegistryHarness r, RecipeVersion version) =>
        (await r.Approvals.Latest(version.Id, version.Version, version.Hash, Token)).Value!;

    private sealed class Capturing : IInstanceRuntime
    {
        public RecipeVersion? Recipe => Started?.Recipe;

        public InstanceStart? Started { get; private set; }

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken)
        {
            Started = start;
            var address = InstanceAddress.From("T-1/skill-trial-review/1");
            return ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, start.Recipe, "run", Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            { Id = WorkflowInstanceId.Of(address), Version = 1 });
        }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OneItem : IItems
    {
        public ValueTask<Result<ResolvedItem>> Resolve(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ResolvedItem>>(new ResolvedItem(ItemKey.From("T-1"), []));
    }
}
