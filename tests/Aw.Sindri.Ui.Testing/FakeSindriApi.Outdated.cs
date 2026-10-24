using System.Net;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.Testing;

internal sealed partial class FakeSindriApi
{
    /// <summary>
    /// A recipe list where one workflow is approved at an older version than the one shipped (version 5 shipped, 4 approved), one is approved and current, and a third is approved but stale with no approved version served (an older Sindri).
    /// </summary>
    /// <returns>The list as the route answers it; pass it as <see cref="RecipeListAnswer"/>.</returns>
    internal static RouteAnswer OutdatedRecipeList()
    {
        var list = JsonNode.Parse(GoldenFiles.Api("workflow-recipes", "list"))!;   // why: the golden is a JSON document
        var recipes = list["recipes"]!.AsArray();
        var shipped = recipes[0]!;
        var older = JsonNode.Parse(shipped.ToJsonString())!;
        older["id"] = "workflow.review";
        older["version"] = 5;
        older["approvedVersion"] = 4;
        older["approvedHash"] = "sha256:" + new string('a', 64);
        var stale = JsonNode.Parse(shipped.ToJsonString())!;
        stale["id"] = "workflow.triage";
        stale["stale"] = new JsonArray("pin-drift");
        recipes.Add(older);
        recipes.Add(stale);
        return new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }
}
