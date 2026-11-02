using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListRecipes;

/// <summary>
/// The registry's register, list and show routes on the real host (engine spec §6.3): POST /workflow/recipes is desk (a
/// session is refused), GET /workflow/recipes and GET /workflow/recipes/{id}@{hash} are queries any caller may read, and
/// the list carries each recipe's inputs for the app's start form (README X34).
/// </summary>
public sealed class RecipeQueryRoutesTests : IAsyncLifetime
{
    private readonly CallerHost _host = new();

    public ValueTask InitializeAsync() => _host.InitializeAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_human_registers_and_any_caller_lists_and_shows()
    {
        var human = Human(_host.Token.Value);
        var body = JsonSerializer.Serialize(new { text = ShippedRecipes.Read("sindri/workflow.step.yaml"), file = "workflow.step.yaml" });

        var registered = await _host.Http.Ask(Post("/sindri/workflow/recipes", body, human));
        var hash = JsonDocument.Parse(registered.Body).RootElement.GetProperty("hash").GetString()!;   // why: a registered response always carries the hash
        var listed = await _host.Http.Ask(Get("/sindri/workflow/recipes", (SindriHeaders.Caller, "session")));
        var stale = await _host.Http.Ask(Get("/sindri/workflow/recipes?stale=true", human));
        var shown = await _host.Http.Ask(Get($"/sindri/workflow/recipes/workflow.step@{hash["sha256:".Length..][..12]}", (SindriHeaders.Caller, "session")));
        var missing = await _host.Http.Ask(Get("/sindri/workflow/recipes/workflow.step@0000000000000000", human));

        Assert.True(registered.Status == HttpStatusCode.OK, registered.Body);
        Assert.True(listed.Status == HttpStatusCode.OK, listed.Body);
        var recipe = Assert.Single(JsonDocument.Parse(listed.Body).RootElement.GetProperty("recipes").EnumerateArray(), r => r.GetProperty("hash").GetString() == hash);   // the shipped recipes nobody registered are listed too (C-12)
        Assert.Equal((hash, "unapproved"), (recipe.GetProperty("hash").GetString(), recipe.GetProperty("standing").GetString()));
        Assert.Equal("fact:spec", Assert.Single(recipe.GetProperty("inputs").EnumerateArray()).GetProperty("key").GetString());
        Assert.DoesNotContain(JsonDocument.Parse(stale.Body).RootElement.GetProperty("recipes").EnumerateArray(), r => r.GetProperty("hash").GetString() == hash);
        Assert.True(shown.Status == HttpStatusCode.OK, shown.Body);
        var view = JsonDocument.Parse(shown.Body).RootElement;
        Assert.Equal((hash, "fact:spec"), (view.GetProperty("hash").GetString(), Assert.Single(view.GetProperty("inputs").EnumerateArray()).GetProperty("key").GetString()));
        Assert.Equal((HttpStatusCode.NotFound, "SND-RECIPE-002"), (missing.Status, missing.Code));
        var ledger = _host.App.Services.GetRequiredService<ILedgerStore>();
        var types = (await ledger.ReadAfter(0, 100, TestContext.Current.CancellationToken)).Value.Select(e => e.Envelope.Type).ToList();
        Assert.Equal(1, types.Count(t => t == EventTypes.RecipeRegistered));
    }
}
