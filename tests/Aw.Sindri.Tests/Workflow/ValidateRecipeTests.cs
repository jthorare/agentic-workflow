using Aw.Kernel.Services;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>recipe validate's slice, resolved from the real composition (the build's reader, catalog and validator).</summary>
public sealed class ValidateRecipeTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-recipe-test");

    [Fact, Trait("Lane", "unit")]
    public async Task Unreachable_state_is_reported_with_its_location()
    {
        var response = await Validate("invalid/RCP-REACH-001.yaml", "RCP-REACH-001.yaml");

        Assert.False(response.Passed);
        Assert.Contains(
            "RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it",
            response.Findings.Select(f => f.ToLine()),
            StringComparer.Ordinal);
    }

    private static async Task<ValidateRecipeResponse> Validate(string fixture, string file)
    {
        var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>();
        var result = await service.Handle(ValidateRecipeRequest.FromText(RecipeFixtures.Read(fixture), file), TestContext.Current.CancellationToken);
        return result.Value;
    }
}
