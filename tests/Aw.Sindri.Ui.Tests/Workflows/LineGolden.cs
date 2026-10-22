using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The instance-list golden turned into the line workflow's rows, as the engine serves them with a line stage and a root address.</summary>
internal static class LineGolden
{
    // The golden's project rows are the line's (the engine serves workflow.line with lineStage and rootAddress on each), and its finished
    // triage rows stand for finished line runs; the other rows stay other workflows' with no line stage.
    internal static string AsLine(string body)
    {
        var list = JsonNode.Parse(body)!;
        foreach (var row in list["instances"]!.AsArray().Select(r => r!.AsObject()))
        {
            var recipe = row["recipe"]!["id"]!.GetValue<string>();
            var state = row["state"]!.GetValue<string>();
            if (recipe == "workflow.project" || (recipe == "workflow.triage-alert" && state == "done"))
            {
                row["recipe"]!["id"] = "workflow.line";
                row["lineStage"] = state == "build" ? "phase" : state;   // the golden calls the build state "build"; the line calls it "phase"
                row["rootAddress"] = row["address"]!.GetValue<string>();
            }
        }

        return list.ToJsonString();
    }
}
