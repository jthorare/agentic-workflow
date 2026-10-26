using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListRecipes;

/// <summary>
/// The recipe list names every recipe this build ships, registered or not (C-12): the app's Workflow types page reads it and has to count the
/// same workflows Settings does, so a shipped recipe nobody has registered yet is listed as not approved.
/// </summary>
public sealed class ShippedRecipesListedTests : IAsyncLifetime
{
    private readonly CallerHost _host = new();

    public ValueTask InitializeAsync() => _host.InitializeAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task GET_recipes_lists_every_shipped_recipe_and_an_unregistered_one_stands_unapproved()
    {
        await using var scope = _host.App.Services.CreateAsyncScope();
        var plan = (await scope.ServiceProvider.GetRequiredService<IShippedRecipes>().Plan(TestContext.Current.CancellationToken)).Value;
        var shippedIds = plan.Recipes.Select(r => r.Id.Value).Concat(plan.OwnApproval.Select(o => o.Id.Value)).Distinct(StringComparer.Ordinal).ToList();

        var listed = await _host.Http.Ask(Get("/sindri/workflow/recipes", (SindriHeaders.Caller, "session")));

        Assert.True(listed.Status == HttpStatusCode.OK, listed.Body);
        Assert.NotEmpty(shippedIds);
        var lines = JsonDocument.Parse(listed.Body).RootElement.GetProperty("recipes").EnumerateArray().ToList();
        var ids = lines.Select(l => l.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(shippedIds, id => !ids.Contains(id));
        Assert.All(lines, l => Assert.Equal("unapproved", l.GetProperty("standing").GetString()));   // why: nothing is registered or approved in this host
    }
}
