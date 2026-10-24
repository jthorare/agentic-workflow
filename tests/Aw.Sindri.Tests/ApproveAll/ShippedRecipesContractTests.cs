using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>
/// The real shipped recipes (the embedded set, the registry and the approvals over the in-memory registry ports) honour the same contract the fake does. Each seed becomes a small recipe
/// whose content carries the seed's hash digit, so a seed with another digit is another hash; a refused seed declares no evals, which the approval checks refuse (SND-RECIPE-008).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShippedRecipesContractTests : ShippedRecipesContract, IAsyncDisposable
{
    private RegistryHarness? _registry;

    protected override IShippedRecipes Create(IReadOnlyList<ShippedSeed> seeds)
    {
        // why: the embedded set sorts by path, and the contract expects ties in seeded order, so the file name starts with the seed's place
        var files = seeds.Select(static (s, i) => new RecipeSetFile(i.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "-" + s.Id.Value + ".yaml", Encoding.UTF8.GetBytes(Yaml(s))));
        _registry = new RegistryHarness(services => services
            .AddSingleton<IEmbeddedRecipes>(new FakeEmbeddedRecipes(files))
            .AddShippedRecipes());
        return new AsHuman(_registry);
    }

    public ValueTask DisposeAsync() => _registry?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static string Yaml(ShippedSeed seed) =>
        SeededRecipeYaml.Of(seed.Id.Value, seed.Version, seed.Hash.Value[^1].ToString(), seed.DisplayName, [.. seed.Children.Select(static c => c.Value)], withEvals: seed.Refusal is null);

    /// <summary>The port as the job runs it: a human's caller context, the lease held, and a scope of its own per call.</summary>
    private sealed class AsHuman(RegistryHarness registry) : IShippedRecipes
    {
        public async ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken)
        {
            await Lease();
            using var caller = registry.H.Callers.Enter(RegistryHarness.Human);
            await using var scope = registry.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IShippedRecipes>().Plan(cancellationToken);
        }

        public async ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken)
        {
            await Lease();
            using var caller = registry.H.Callers.Enter(RegistryHarness.Human);
            await using var scope = registry.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IShippedRecipes>().Approve(plan, progress, cancellationToken);
        }

        private async Task Lease()
        {
            if (registry.H.Held.Current is null)
            {
                _ = await registry.H.HoldLease();
            }
        }
    }
}
