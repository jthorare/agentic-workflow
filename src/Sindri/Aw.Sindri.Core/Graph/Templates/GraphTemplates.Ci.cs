using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Templates;

public static partial class GraphTemplates
{
    private const string CiNode = "ci";

    /// <summary>Says whether a starter template can read a repo's CI (weekday-check and daily-digest), so a caller looks the source up only for those.</summary>
    /// <param name="name">The template's name.</param>
    /// <returns>True when <see cref="Find"/> builds a CI variant for the name.</returns>
    public static bool ReadsCi(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Equals(name, "weekday-check", StringComparison.Ordinal) || string.Equals(name, "daily-digest", StringComparison.Ordinal);
    }

    // why: the plain template stays the one source of truth; the variant adds a 'ci' call_tool step after the schedule and swaps the prompt's text, so nothing else drifts
    private static GraphTemplate? CiTemplate(string name, GitHubCiSource ci)
    {
        // why: a branch name may hold braces, and '{{' in the step's arguments or the prompt is an expression that fails validation; such a repo gets the plain template, as one with no connection does
        if (!ReadsCi(name) || ci.Branch.Contains('{', StringComparison.Ordinal) || ci.Branch.Contains('}', StringComparison.Ordinal) || Find(name) is not { } plain)
        {
            return null;
        }

        var schedule = plain.Spec.Nodes.First(static n => n.Type == "schedule");
        var prompt = plain.Spec.Nodes.First(static n => n.Type == "prompt");
        var step = new GraphNode(NodeId.From(CiNode), "call_tool", "Read the branch's CI", new JsonObject
        {
            ["connection"] = ci.Connection,
            ["tool"] = "list_runs",
            ["arguments"] = new JsonObject { ["owner"] = ci.Owner, ["repo"] = ci.Repo, ["branch"] = ci.Branch, ["per_page"] = "5" },
        });
        var config = (JsonObject)prompt.Config.DeepClone();
        config["prompt"] = CiPrompt(name, schedule.Id.Value, ci.Branch);
        var nodes = new List<GraphNode>();
        foreach (var node in plain.Spec.Nodes)
        {
            nodes.Add(node == prompt ? prompt with { Config = config } : node);
            if (node == schedule)
            {
                nodes.Add(step);
            }
        }

        return plain with { Spec = plain.Spec with { Nodes = nodes, Edges = CiEdges(plain.Spec.Edges, schedule.Id, step.Id, prompt.Id) } };
    }

    private static string CiPrompt(string name, string scheduleId, string branch)
    {
        var runs = $"These are the latest CI runs on the branch, newest first: {{{{{CiNode}.result}}}}.";
        var none = $"When there are no runs, count the run as passed and mention: no CI runs on {branch}.";
        return string.Equals(name, "weekday-check", StringComparison.Ordinal)
            ? $"Today is {{{{{scheduleId}.scheduledFor}}}}. {runs} Look at the newest run. Set flagged to true when it did not succeed or when today is a Friday. Set summary to one line: when the run did not succeed, name the failing workflow; when today is a Friday, add: Friday: review your open pull requests before the weekend. {none} When neither applies, set flagged to false and summary to: CI is green."
            : $"Today is {{{{{scheduleId}.scheduledFor}}}}. {runs} Write one short line for the start of the day: the weekday, one thing worth doing on that day of the week, and whether the newest run passed (name the workflow when it did not). {none}";
    }

    private static List<GraphEdge> CiEdges(IReadOnlyList<GraphEdge> plainEdges, NodeId scheduleId, NodeId stepId, NodeId promptId)
    {
        var edges = new List<GraphEdge>();
        foreach (var edge in plainEdges)
        {
            if (edge.From == scheduleId && edge.To == promptId)
            {
                edges.Add(edge with { To = stepId });
                edges.Add(new GraphEdge(stepId, Output: null, promptId));
            }
            else
            {
                edges.Add(edge);
            }
        }

        return edges;
    }
}
