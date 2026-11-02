using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Workflow;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// The approve, revoke and challenge routes on the real host (engine spec §6.3): '{id}@{hash}' binds in minimal APIs
/// with the hash as 'sha256:…' or its 64 digits, a human at a terminal approves and revokes through the challenge,
/// a session is refused, and a malformed hash is a 400.
/// </summary>
public sealed class RecipeRoutesTests : IAsyncLifetime
{
    private readonly CallerHost _host = new();

    public ValueTask InitializeAsync() => _host.InitializeAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Human_approves_and_revokes_through_the_routes_without_a_typed_challenge()
    {
        var step = await Register();
        var human = Human(_host.Token.Value);

        var issued = await Challenge("approve workflow.step 5", human);
        var approved = await _host.Http.Ask(Post($"/sindri/workflow/recipes/workflow.step@{step.Hash.Digest}/approve", Answer(issued), human));
        var revoke = await Challenge($"revoke workflow.step@{step.Hash.Display}", human);
        var revoked = await _host.Http.Ask(Post($"/sindri/workflow/recipes/workflow.step@{step.Hash.Value}/revoke", Answer(revoke), human));

        Assert.True(approved.Status == HttpStatusCode.OK, approved.Body);
        Assert.Equal(step.Hash.Value, JsonDocument.Parse(approved.Body).RootElement.GetProperty("approval").GetProperty("hash").GetString());
        Assert.True(revoked.Status == HttpStatusCode.OK, revoked.Body);
        var ledger = _host.App.Services.GetRequiredService<ILedgerStore>();
        var types = (await ledger.ReadAfter(0, 100, TestContext.Current.CancellationToken)).Value.Select(e => e.Envelope.Type).ToList();
        Assert.Equal(1, types.Count(t => t == EventTypes.RecipeApproved));
        Assert.Equal(1, types.Count(t => t == EventTypes.RecipeRevoked));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("workflow.step@nothex")]
    [InlineData("Not An Id@0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public async Task Malformed_id_or_hash_in_the_route_is_a_400(string segment)
    {
        var answer = await _host.Http.Ask(Post($"/sindri/workflow/recipes/{Uri.EscapeDataString(segment).Replace("%40", "@", StringComparison.Ordinal)}/approve",
            """{"challenge":"CHL-00000000000000000000000000000000","typed":"approve workflow.step 5"}""", Human(_host.Token.Value)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (answer.Status, answer.Code));
    }

    private async Task<RegisteredRecipe> Register()
    {
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Document!;   // why: the shipped recipes bind (ShippedRecipesTests)
        var text = Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null }, SliceHarness.Json));
        var store = _host.App.Services.GetRequiredService<IRecipeStore>();
        return (await store.Add(new RegisteredRecipe(document.Recipe.Id, document.Recipe.Version, document.Hash, text, Base: null, DateTimeOffset.UnixEpoch),
            TestContext.Current.CancellationToken)).Value.Recipe;
    }

    private async Task<(string Challenge, string Action)> Challenge(string action, (string Name, string Value)[] human)
    {
        var issued = await _host.Http.Ask(Post("/sindri/workflow/challenges", $$"""{"action":"{{action}}"}""", human));
        Assert.True(issued.Status == HttpStatusCode.OK, issued.Body);
        var root = JsonDocument.Parse(issued.Body).RootElement;
        return (root.GetProperty("challenge").GetString()!, root.GetProperty("action").GetString()!);   // why: an issued challenge always carries both
    }

    private static string Answer((string Challenge, string Action) issued) =>
        $$"""{"challenge":"{{issued.Challenge}}","typed":"{{issued.Action}}"}""";
}
