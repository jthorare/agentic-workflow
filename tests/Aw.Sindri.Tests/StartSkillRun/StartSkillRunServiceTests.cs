using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.RegistryWrites;
using Aw.Sindri.Features.StartSkillRun;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Sandbox.Egress;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.StartSkillRun;

/// <summary>'sindri skill run' refuses an input that doesn't fit its block before any recipe is made, and stores what a successful start carries (skill flows spec §7).</summary>
[Lane(TestLane.Unit)]
public sealed class StartSkillRunServiceTests
{
    // The fixture review block plus an optional free-text input and an optional input with a declared list.
    private const string Rich = "schema: 1\nblocks:\n  - id: review\n    sentence: Reviews a pull request.\n    inputs:\n      pr: { type: text, label: Pull request (number or link), required: true }\n      note: { type: text, label: A note, required: false }\n      mode: { type: text, label: Mode, required: false, values: [mvp, growth] }\n    args: [\"{pr}\", { flag: --note, input: note }, { flag: --mode, input: mode }]\n    outputs: { findings: { type: path, from: review_file } }\n    outcomes:\n      from: verdict\n      map: { PASS: pass }\n      success: pass\n    pushes: false\n    maxDuration: 2h\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_start_with_no_caller_is_refused()
    {
        await using var r = await Started();

        var run = await Run(r, "review", Pr(), skills: null, caller: false);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);
    }

    [Fact]
    public async Task A_block_that_failed_to_load_is_refused_with_its_reason()
    {
        using var folder = Folder(Rich.Replace("args: [\"{pr}\", { flag: --note, input: note }, { flag: --mode, input: mode }]", "args: [\"{missing}\"]", StringComparison.Ordinal));
        await using var r = await Started();

        var run = await Run(r, "review", Pr(), folder.Path);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Contains("is not loaded:", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("missing", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);
    }

    [Fact]
    public async Task A_block_nobody_loaded_is_refused_as_unknown()
    {
        await using var r = await Started();

        var run = await Run(r, "nope", Pr(), skills: null);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Contains("no skill block 'nope'", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);
    }

    [Fact]
    public async Task An_input_the_block_does_not_declare_is_refused_naming_it()
    {
        await using var r = await Started();

        var run = await Run(r, "review", Pr(("colour", "red")), skills: null);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Contains("has no input 'colour'", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_required_input_that_is_missing_or_blank_is_refused(string? pr)
    {
        await using var r = await Started();
        var inputs = pr is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = pr };

        var run = await Run(r, "review", inputs, skills: null);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Contains("needs input 'pr'", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);
    }

    [Theory]
    [InlineData("not-a-pr")]
    [InlineData("0")]
    [InlineData("https://example.com/owner/name/pull/4")]
    public async Task A_malformed_pr_is_refused_before_any_recipe_is_registered_or_approved(string pr)
    {
        await using var r = await Started();

        var run = await Run(r, "review", Pr(("pr", pr)), skills: null);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.SkillPrInputInvalid.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);   // why: the check runs before Approve, so a typo leaves no trial recipe behind
    }

    [Fact]
    public async Task A_value_outside_the_inputs_list_is_refused_listing_the_allowed_ones()
    {
        using var folder = Folder(Rich);
        await using var r = await Started();

        var run = await Run(r, "review", Pr(("mode", "pivot")), folder.Path);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Contains("input 'mode' of block 'review' is one of: mvp, growth", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Null(run.Runtime.Started);
        await NothingWritten(r);
    }

    [Fact]
    public async Task A_value_on_the_inputs_list_starts()
    {
        using var folder = Folder(Rich);
        await using var r = await Started();

        var run = await Run(r, "review", Pr(("mode", "growth")), folder.Path);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
        Assert.NotNull(run.Runtime.Started);
    }

    [Fact]
    public async Task A_value_over_one_mebibyte_is_refused_naming_its_fact()
    {
        using var folder = Folder(Rich);
        await using var r = await Started();

        var run = await Run(r, "review", Pr(("note", new string('x', IFactStore.MaxBytes + 1))), folder.Path);

        Assert.True(run.Result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, run.Result.Error.Code, StringComparer.Ordinal);
        Assert.Contains("fact:note is larger than 1 MiB", run.Result.Error.Message, StringComparison.Ordinal);
        Assert.Null(run.Runtime.Started);
    }

    [Fact]
    public async Task A_value_of_exactly_one_mebibyte_starts()
    {
        using var folder = Folder(Rich);
        await using var r = await Started();

        var run = await Run(r, "review", Pr(("note", new string('x', IFactStore.MaxBytes))), folder.Path);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
    }

    [Fact]
    public async Task A_run_with_a_skills_folder_carries_the_folder_text_as_a_start_fact()
    {
        using var folder = Folder(Rich);
        await using var r = await Started();

        var run = await Run(r, "review", Pr(), folder.Path);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
        var fact = run.Runtime.Started!.Inputs[FactKey.From(SkillTrialRecipe.SkillsFolderFact)];
        Assert.Equal(folder.Path, await Read(r, fact));   // why: dropped, the trial would silently run the installed skill
        Assert.Equal("42", await Read(r, run.Runtime.Started.Inputs[FactKey.From("fact:pr")]));
    }

    [Fact]
    public async Task A_run_without_a_skills_folder_carries_no_folder_fact()
    {
        await using var r = await Started();

        var run = await Run(r, "review", Pr(), skills: null);

        Assert.True(run.Result.IsSuccess, Why(run.Result));
        Assert.DoesNotContain(FactKey.From(SkillTrialRecipe.SkillsFolderFact), run.Runtime.Started!.Inputs.Keys);
        Assert.Equal("42", await Read(r, run.Runtime.Started.Inputs[FactKey.From("fact:pr")]));
    }

    private static Dictionary<string, string> Pr(params (string Key, string Value)[] more)
    {
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" };
        foreach (var (key, value) in more)
        {
            inputs[key] = value;
        }

        return inputs;
    }

    private static string Why(Result<StartSkillRunResponse> result) => result.IsError ? result.Error.ToString() : string.Empty;

    private static async Task NothingWritten(RegistryHarness r)
    {
        Assert.Empty((await r.Recipes.All(Token)).Value);
        Assert.Empty((await r.Approvals.Current(Token)).Value);
    }

    private static async Task<string> Read(RegistryHarness r, InputFact fact)
    {
        await using var scope = r.Services.CreateAsyncScope();
        var bytes = await scope.ServiceProvider.GetRequiredService<InMemoryFactStore>().Read(fact.Value, Token);
        return Encoding.UTF8.GetString(bytes.Value.Span);
    }

    private static TempDirectory Folder(string yaml)
    {
        var folder = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(folder.Path, "review"));
        File.WriteAllText(Path.Combine(folder.Path, "review", "block.yaml"), yaml);
        File.WriteAllText(Path.Combine(folder.Path, "review", "SKILL.md"), File.ReadAllText(Path.Combine(SkillFixtures.Folder, "review", "SKILL.md")));
        return folder;
    }

    // Serve's loaded set is the fixture folder; its descriptors are in the catalog the registry validates against.
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

    private static async Task<(Result<StartSkillRunResponse> Result, Capturing Runtime)> Run(RegistryHarness r, string block, IReadOnlyDictionary<string, string> inputs, string? skills, bool caller = true)
    {
        var runtime = new Capturing();
        using var entered = caller ? r.H.Callers.Enter(RegistryHarness.Human) : null;
        await using var scope = r.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var service = new StartSkillRunService(new SkillBlockRegistry(SkillFixtures.Folder), new OneItem(), sp.GetRequiredService<TrialRecipes>(), sp.GetRequiredService<InMemoryFactStore>(), runtime, TimeProvider.System, r.H.Callers);
        return (await service.Handle(new StartSkillRunRequest(block, "T-1", inputs, skills), Token), runtime);
    }

    private sealed class Capturing : IInstanceRuntime
    {
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
