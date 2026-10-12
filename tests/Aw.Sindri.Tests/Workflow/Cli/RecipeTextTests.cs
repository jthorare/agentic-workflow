using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands.Recipe;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>'recipe validate''s plain and --json output, from a response (spec §6.3: rule lines, then the hash).</summary>
public sealed class RecipeTextTests
{
    private static readonly RecipeHash _hash = RecipeHash.From("sha256:" + new string('a', 64));

    [Fact, Trait("Lane", "unit")]
    public void Errors_come_first_then_information_then_skipped_then_the_hash()
    {
        var response = new ValidateRecipeResponse(
            [Finding("RCP-RES-001", FindingSeverity.Information), Finding("RCP-REACH-001", FindingSeverity.Error),
             Finding("RCP-EFFECT-001", FindingSeverity.Skipped), Finding("RCP-BLOCK-001", FindingSeverity.Error)],
            _hash);

        var text = RecipeText.Validation(response);

        Assert.Equal(string.Join('\n',
            "RCP-REACH-001: what at r.yaml:1:1 (states.x); fix",
            "RCP-BLOCK-001: what at r.yaml:1:1 (states.x); fix",
            "information: RCP-RES-001: what at r.yaml:1:1 (states.x); fix",
            "skipped: RCP-EFFECT-001: what at r.yaml:1:1 (states.x); fix",
            "hash " + _hash.Value,
            string.Empty), text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_that_did_not_bind_has_no_hash_line()
    {
        var text = RecipeText.Validation(new ValidateRecipeResponse([Finding("RCP-SCHEMA-001", FindingSeverity.Error)], Hash: null));

        Assert.Equal("RCP-SCHEMA-001: what at r.yaml:1:1 (states.x); fix\n", text);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_json_view_names_each_field_and_keeps_the_plain_order()
    {
        var response = new ValidateRecipeResponse(
            [Finding("RCP-RES-001", FindingSeverity.Information), Finding("RCP-REACH-001", FindingSeverity.Error)], Hash: null);

        var view = RecipeValidationView.From(response, SliceHarness.Json);

        Assert.False(view.Passed);
        Assert.Null(view.Normalized);
        Assert.Equal(["RCP-REACH-001", "RCP-RES-001"], view.Findings.Select(f => f.Code), StringComparer.Ordinal);
        Assert.Equal(new RecipeFindingView("RCP-REACH-001", FindingSeverity.Error, "what", "r.yaml", 1, 1, "states.x", "fix"), view.Findings[0]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sindri/workflow.step.yaml")]
    [InlineData("sindri/workflow.item.yaml")]
    public async Task The_normalized_long_form_hashes_to_the_hash(string recipe)
    {
        var stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-recipe-text-test");
        var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var response = (await services.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>()
            .Handle(ValidateRecipeRequest.FromText(ShippedRecipes.Read(recipe), Path.GetFileName(recipe)), TestContext.Current.CancellationToken)).Value;

        var view = RecipeValidationView.From(response, services.GetRequiredService<System.Text.Json.JsonSerializerOptions>());

        Assert.NotNull(view.Normalized);
        Assert.False(view.Normalized.ContainsKey("hash"));
        Assert.Equal(response.Hash, RecipeHash.Of(CanonicalJson.Of(view.Normalized)));
    }

    private static RecipeFinding Finding(string rule, FindingSeverity severity) => new(rule, "what", "r.yaml", 1, 1, "states.x", "fix", severity);
}
