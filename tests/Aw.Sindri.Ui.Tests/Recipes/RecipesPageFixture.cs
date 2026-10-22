using System.Net;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>What the Recipes page tests share: a list of three workflows (approved, not approved, approved but out of date) served as JSON, and a page over the fake.</summary>
internal sealed class RecipesPageFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    private const string Hash = "sha256:e204c3b4ff1755b975bc92713ab9a7f4ca9ea54a8888a5b32b06bba4fac8c636";

    internal FakeSindriApi Server { get; } = new();

    internal static string Row(string id, string standing = "approved", string stale = "") =>
        $$"""{"id":"{{id}}","version":1,"hash":"{{Hash}}","standing":"{{standing}}","via":"desk","stale":[{{stale}}],"inputs":[],"urgency":"work","outputs":[],"orderedBy":"path","states":[{"id":"a","order":1,"branchOf":null}],"loops":[],"name":null}""";

    internal void Serve(params string[] rows) => Server.RecipeListAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"recipes\":[" + string.Join(',', rows) + "]}");

    internal void ServeThree() => Serve(Row("workflow.phase"), Row("workflow.pr-babysitter", "unapproved"), Row("workflow.review-loop", stale: "\"child-stale\""));

    internal RecipesPageViewModel Page() => new(Server);

    internal async Task<RecipesPageViewModel> Shown(CancellationToken token)
    {
        var page = Page();
        await page.Refresh(Url, token);
        page.SetShown(shown: true);
        await page.CatchUp;
        return page;
    }
}
