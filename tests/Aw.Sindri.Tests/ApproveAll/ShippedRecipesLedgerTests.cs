using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Challenges;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>
/// The owner's bug, end to end on a ledger file: a box whose recipes were approved once and then shipped again at a newer version showed "everything approved" and nothing re-approved
/// them. Approve v1 of a recipe and the one that uses it, ship v2 of the child, and the plan lists the child as updated and the parent as needing a second look, the child first; applying it
/// approves both, and the next plan is empty.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ShippedRecipesLedgerTests : IAsyncDisposable
{
    private static readonly RecipeId _child = RecipeId.From("workflow.step");
    private static readonly RecipeId _parent = RecipeId.From("workflow.item");
    private static readonly CommandIdSource _ids = new();

    private readonly TempHome _home = new();
    private readonly LedgerDatabase _database;
    private readonly SwitchableEmbedded _embedded = new();
    private readonly RegistryHarness _registry;

    public ShippedRecipesLedgerTests()
    {
        _database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider());
        var store = new SqliteRecipeStore(_database);
        var approvals = new SqliteRecipeApprovals(_database);
        _registry = new RegistryHarness(services => services
            .AddSingleton<IRecipeStore>(store)
            .AddSingleton<IRecipeApprovals>(approvals)
            .AddSingleton<IEmbeddedRecipes>(_embedded)
            .AddShippedRecipes());
    }

    public async ValueTask DisposeAsync()
    {
        await _registry.DisposeAsync();
        _database.Dispose();
        _home.Dispose();
    }

    [Fact]
    public async Task A_newer_shipped_version_is_planned_updated_children_first_applied_and_then_the_plan_is_empty()
    {
        _ = await _registry.H.HoldLease();
        _embedded.Ship(("workflow.step", 1, "a", []), ("workflow.item", 1, "a", ["workflow.step"]));
        var first = await Plan();
        Assert.Equal([_child, _parent], first.Recipes.Select(static r => r.Id));
        Assert.All(first.Recipes, static r => Assert.Equal(PlanChange.New, r.Change));
        var firstResult = await Apply(first);
        Assert.True(firstResult.Approved.Count == 2, string.Join("; ", firstResult.Refused.Select(static r => r.Code + " " + r.Sentence)));
        Assert.True((await Plan()).IsEmpty);

        _embedded.Ship(("workflow.step", 2, "b", []), ("workflow.item", 1, "a", ["workflow.step"]));
        var second = await Plan();

        Assert.Equal([(_child, PlanChange.Updated), (_parent, PlanChange.SecondLook)], second.Recipes.Select(static r => (r.Id, r.Change)));
        Assert.Equal(first.Recipes[0].Hash, second.Recipes[0].PreviousApprovedHash);
        var result = await Apply(second);
        Assert.Equal([_child, _parent], result.Approved.Select(static a => a.Id));
        Assert.Empty(result.Refused);
        Assert.True((await Plan()).IsEmpty);
        var events = _registry.Events(EventTypes.RecipeApproved).Select(e => _registry.H.Events.Payload<RecipeApproved>(e).Value).TakeLast(2).ToList();
        Assert.All(events, e => Assert.Equal((second.Digest, _ids.Batch.ToString()), (e.PlanDigest, e.Batch)));
    }

    [Fact]
    public async Task A_revoked_shipped_hash_stays_out_of_the_plan()
    {
        _ = await _registry.H.HoldLease();
        _embedded.Ship(("workflow.step", 1, "a", []));
        var plan = await Plan();
        _ = await Apply(plan);
        var hash = plan.Recipes[0].Hash;
        _ = await new SqliteRecipeApprovals(_database).Revoke(new RecipeRevocation(_child, 1, hash, "human:cli", DateTimeOffset.UnixEpoch, CascadedFrom: null), TestContext.Current.CancellationToken);

        Assert.True((await Plan()).IsEmpty);
    }

    private async Task<ShippedRecipePlan> Plan()
    {
        using var entered = _registry.H.Callers.Enter(RegistryHarness.Human);
        await using var scope = _registry.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IShippedRecipes>().Plan(TestContext.Current.CancellationToken)).Value;
    }

    private async Task<ApproveAllResult> Apply(ShippedRecipePlan plan)
    {
        var challenges = new InMemoryChallenges(new FakeTimeProvider(KnownInstants.Epoch));
        var issued = challenges.Issue(ChallengeAction.ApproveAll(plan.Digest), ChallengeSubject.ForApproveAll(plan.Digest));
        var redeemed = RedeemedPlan.Redeem(challenges, new ChallengeAnswer(issued.Id, "approve-all " + plan.Digest), plan, _ids.Batch).Value;
        using var entered = _registry.H.Callers.Enter(RegistryHarness.Human);
        await using var scope = _registry.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IShippedRecipes>().Approve(redeemed, new Progress<string>(), TestContext.Current.CancellationToken)).Value;
    }

    private sealed class CommandIdSource
    {
        public Core.Ids.CommandId Batch { get; } = Core.Ids.CommandId.From(new Guid(192, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>An embedded set a test replaces between plans, as a new build would.</summary>
    private sealed class SwitchableEmbedded : IEmbeddedRecipes
    {
        private List<RecipeSetFile> _files = [];

        public void Ship(params (string Id, int Version, string Content, string[] Children)[] recipes) =>
            _files = [.. recipes.Select(r => new RecipeSetFile(r.Id + ".yaml", Encoding.UTF8.GetBytes(SeededRecipeYaml.Of(r.Id, r.Version, r.Content, "Recipe for " + r.Id["workflow.".Length..], r.Children, withEvals: true))))];

        public IReadOnlyList<RecipeSetFile> Files() => _files;
    }
}
