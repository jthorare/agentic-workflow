using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.ShowRecipe;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Challenges;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// The registry slices behind the real command chain (AddCommand) and the Host's catalog, over in-memory ports: the
/// recipe store, the approvals, the challenges and a recording wake (engine spec Phase 2). P2's units register their
/// slices here as they ship them.
/// </summary>
internal sealed class RegistryHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly NoInstances _instances = new();

    public RegistryHarness(Action<IServiceCollection>? configure = null)
    {
        Challenges = new InMemoryChallenges(H.Clock);
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(H.Clock)
            .AddSingleton<Core.Views.IProjectorStatus>(new Core.Views.ProjectorProgress(H.Clock))
            .AddSingleton<IIdGenerator>(H.Ids)
            .AddSingleton<ILedgerStore>(H.Store)
            .AddSingleton<ILease>(H.Store)
            .AddSingleton(H.Held)
            .AddSingleton(H.Events)
            .AddSingleton(H.Callers)
            .AddSingleton<ICommandCatalog>(new CommandCatalog())
            .AddSingleton<Core.Views.IProjectorStatus>(new Core.Views.ProjectorProgress(H.Clock))
            .AddSingleton<ILogger<AuditTrail>>(new ListLogger<AuditTrail>())
            .AddSingleton<IRecipeStore>(Recipes)
            .AddSingleton<IRecipeApprovals>(Approvals)
            .AddSingleton<IChallenges>(Challenges)
            .AddSingleton<IRecipeWake>(Wake)
            .AddSingleton<Core.Workflow.Engine.Runtime.IInstanceDirectory>(_instances)
            .AddSingleton<Core.Workflow.Engine.Runtime.IInstanceRuntime>(_instances)
            .AddSingleton<Core.Workflow.Engine.Runtime.IInstanceReader>(_instances)
            .AddSingleton(SliceHarness.Json)
            .AddSingleton<IProfileSource>(H.Profiles)
            .AddEffectPorts(H)
            .AddRecipeBlocks()
            .AddSingleton<BlockCatalog>().AddSingleton<StepInputRuntimes>()
            .AddScoped<RecipeApprovalPlanner>().AddScoped<RecipeRevokePlanner>()
            .AddScoped<IRecipeProfileFacts, ApprovedRecipeProfileFacts>()
            .AddScoped<RecipeRegistryReader>()
            .AddSingleton(Workflow.RecipeReaderTests.Reader)
            .AddSingleton<Core.Events.RecipeEvents>()
            .AddScoped<IValidator<ApproveRecipeRequest>, AcceptAllValidator<ApproveRecipeRequest>>().AddCommand<ApproveRecipeRequest, ApproveRecipeResponse, ApproveRecipeService>()
            .AddScoped<IValidator<RevokeRecipeRequest>, AcceptAllValidator<RevokeRecipeRequest>>().AddCommand<RevokeRecipeRequest, RevokeRecipeResponse, RevokeRecipeService>()
            .AddScoped<IValidator<IssueChallengeRequest>, AcceptAllValidator<IssueChallengeRequest>>().AddIssue<IssueChallengeRequest, IssueChallengeResponse, IssueChallengeService>()
            .AddScoped<IValidator<RegisterRecipeRequest>, AcceptAllValidator<RegisterRecipeRequest>>().AddCommand<RegisterRecipeRequest, RegisterRecipeResponse, RegisterRecipeService>()
            .AddScoped<IValidator<ListRecipesRequest>, AcceptAllValidator<ListRecipesRequest>>().AddQuery<ListRecipesRequest, ListRecipesResponse, ListRecipesService>()
            .AddScoped<IValidator<ShowRecipeRequest>, AcceptAllValidator<ShowRecipeRequest>>().AddQuery<ShowRecipeRequest, RecipeView, ShowRecipeService>()
            .AddSingleton<Core.Workflow.Engine.Runtime.IApprovedRecipes>(sp => new RegistryApprovedRecipes(Recipes, Approvals, sp.GetRequiredService<BlockCatalog>(), SliceHarness.Json));
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public IServiceProvider Services => _provider;

    public SliceHarness H { get; } = new();

    public InMemoryRecipeStore Recipes { get; } = new();

    public InMemoryRecipeApprovals Approvals { get; } = new();

    public InMemoryChallenges Challenges { get; }

    public RecordingRecipeWake Wake { get; } = new();

    /// <summary>Registers <paramref name="document"/> in the store as RegisterRecipe would keep it: its normalized long form by hash.</summary>
    public async Task<RegisteredRecipe> Register(RecipeDocument document)
    {
        var recipe = document.Recipe;
        var text = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, SliceHarness.Json));
        return (await Recipes.Add(new RegisteredRecipe(recipe.Id, recipe.Version, document.Hash, text, recipe.Base, H.Clock.GetUtcNow()),
            TestContext.Current.CancellationToken)).Value.Recipe;
    }

    /// <summary>Registers <paramref name="recipe"/> under the hash of its normalized long form.</summary>
    public async Task<RegisteredRecipe> Register(Recipe recipe)
    {
        var hash = RecipeHashing.Of(recipe, SliceHarness.Json);
        var text = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, SliceHarness.Json));
        return (await Recipes.Add(new RegisteredRecipe(recipe.Id, recipe.Version, hash, text, recipe.Base, H.Clock.GetUtcNow()),
            TestContext.Current.CancellationToken)).Value.Recipe;
    }

    /// <summary>The recipe a registered row's normalized text reads back as.</summary>
    public static Recipe Parse(RegisteredRecipe registered) => AwJson.Deserialize<Recipe>(Encoding.UTF8.GetBytes(registered.Text), SliceHarness.Json).Value;

    /// <summary>Runs <paramref name="request"/> through its registered chain as <paramref name="caller"/>.</summary>
    public async Task<Result<TResponse>> Run<TRequest, TResponse>(TRequest request, CallerContext caller)
        where TRequest : notnull
    {
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A human at a terminal, as the CLI calls.</summary>
    public static CallerContext Human { get; } = A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    /// <summary>Registers the shipped recipe at <paramref name="path"/> (relative to recipes/, e.g. 'sindri/workflow.step.yaml').</summary>
    public Task<RegisteredRecipe> Shipped(string path) =>
        Register(Workflow.RecipeReaderTests.Reader.Read(ShippedRecipes.Read(path), Path.GetFileName(path)).Document!);   // why: the shipped recipes bind (ShippedRecipesTests)

    /// <summary>The child recipes a served validation resolves (RecipeRegistryReader.Children).</summary>
    public async Task<IChildRecipeResolver> Children()
    {
        await using var scope = _provider.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<RecipeRegistryReader>().Children(TestContext.Current.CancellationToken)).Value;
    }

    /// <summary>Asks for a challenge for <paramref name="action"/> as <paramref name="caller"/> (a human at a terminal by default).</summary>
    public Task<Result<IssueChallengeResponse>> Issue(string action, CallerContext? caller = null) =>
        Run<IssueChallengeRequest, IssueChallengeResponse>(new IssueChallengeRequest(action), caller ?? Human);

    /// <summary>Approves <paramref name="recipe"/> as a human would: a challenge, its action typed back.</summary>
    public async Task<Result<ApproveRecipeResponse>> Approve(RegisteredRecipe recipe)
    {
        var issued = (await Issue(string.Create(CultureInfo.InvariantCulture, $"approve {recipe.Id.Value} {recipe.Version}"))).Value;
        return await Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(recipe.Id, recipe.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), Human);
    }

    /// <summary>
    /// Records the approval <paramref name="recipe"/> was given in the past, with the pins this build computes, without the planner's checks:
    /// for a row the planner now refuses (a legacy-shape one approved before it did).
    /// </summary>
    public async Task ApproveAsInThePast(RegisteredRecipe recipe)
    {
        var parsed = recipe.Read(SliceHarness.Json).Value;
        var current = (await Approvals.Current(TestContext.Current.CancellationToken)).Value;
        var pins = PinRules.Pins(parsed, _provider.GetRequiredService<BlockCatalog>(), current, SliceHarness.Json);
        _ = await Approvals.Approve(RegistryData.Approval(recipe, pins), TestContext.Current.CancellationToken);
    }

    /// <summary>Registers <paramref name="text"/> through RegisterRecipe as <paramref name="caller"/> (a human at a terminal by default).</summary>
    public Task<Result<RegisterRecipeResponse>> RegisterText(string text, string file, CallerContext? caller = null) =>
        Run<RegisterRecipeRequest, RegisterRecipeResponse>(new RegisterRecipeRequest(text, file), caller ?? Human);

    /// <summary>Registers the shipped recipe at <paramref name="path"/> through RegisterRecipe.</summary>
    public Task<Result<RegisterRecipeResponse>> RegisterShipped(string path) => RegisterText(ShippedRecipes.Read(path), Path.GetFileName(path));

    /// <summary>Lists the registered recipes (only the stale ones with <paramref name="stale"/>).</summary>
    public async Task<IReadOnlyList<RecipeSummary>> List(bool stale = false)
    {
        var listed = await Run<ListRecipesRequest, ListRecipesResponse>(new ListRecipesRequest(stale), Human);
        Assert.True(listed.IsSuccess, listed.IsError ? listed.Error.ToString() : null);
        return listed.Value.Recipes;
    }

    /// <summary>The one listed line of <paramref name="recipe"/>.</summary>
    public async Task<RecipeSummary> Line(RegisteredRecipe recipe) => Assert.Single(await List(), s => s.Hash == recipe.Hash);

    /// <summary>Shows <paramref name="id"/>@<paramref name="hash"/> as the API's recipe view; with no hash, the approved version of <paramref name="id"/>.</summary>
    public Task<Result<RecipeView>> Show(RecipeId id, string? hash = null) => Run<ShowRecipeRequest, RecipeView>(new ShowRecipeRequest(id, hash), Human);

    /// <summary>The approval preview of the registered version of <paramref name="id"/> that <paramref name="hash"/> names ('recipe approve' prints it), whether or not it is approved.</summary>
    public async Task<Result<ApprovalPreview>> Preview(RecipeId id, string hash)
    {
        await using var scope = _provider.CreateAsyncScope();
        var planner = scope.ServiceProvider.GetRequiredService<RecipeApprovalPlanner>();
        var found = await planner.Find(id, hash, TestContext.Current.CancellationToken);
        return found.IsError ? found.Error : await planner.Preview(found.Value, TestContext.Current.CancellationToken);
    }

    /// <summary>The ledger's events of <paramref name="type"/>.</summary>
    public IReadOnlyList<LedgerEvent> Events(string type) => [.. H.Store.Events.Where(e => e.Envelope.Type == type)];

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}

/// <summary>The effect blocks' ports, in memory (engine P6): the approved allowlist, the claim, its marker ref, liveness, fact contents and the desk's notices.</summary>
file static class EffectPortRegistrations
{
    public static IServiceCollection AddEffectPorts(this IServiceCollection services, SliceHarness h) => services
        .AddSingleton<IApprovedProfile>(h.Profiles)
        .AddSingleton<Core.Workflow.Engine.Effects.IWorkflowClaims>(new InMemoryWorkflowClaims())
        .AddSingleton<Core.Workflow.Engine.Effects.IClaimRefs>(new InMemoryClaimRefs())
        .AddSingleton<Core.Workflow.Engine.Runtime.IInstanceLiveness>(new FakeInstanceLiveness())
        .AddSingleton<Core.Workflow.Engine.Runtime.IFactContents>(new InMemoryFactStore())
        .AddSingleton<Core.Workflow.Engine.Effects.IDeskNotices>(new InMemoryDeskNotices())
        .AddSingleton<Core.LensPacks.IChangedPaths>(new Testing.LensPacks.FakeChangedPaths())
        .AddSingleton<Core.Workflow.Engine.Runtime.IDiffContent>(new FakeDiffContent())
        .AddSingleton<Core.Scoping.IBriefFiles>(new Testing.Scoping.InMemoryBriefFiles())
        .AddSingleton<Core.Scoping.IRepoCheckouts>(new Testing.Scoping.InMemoryRepoCheckouts())
        .AddSingleton<Core.Forge.IPullRequests>(new Testing.Forge.FakePullRequests(["forge.test"]))
        .AddSingleton<Core.Text.ITextScrubber>(new Testing.PassThroughScrubber());
}
