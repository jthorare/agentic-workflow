using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.SearchWorkflowDocs;

namespace Aw.Sindri.Host.Commands;

/// <summary>The text of 'sindri wf list|show|runs|run-show' (without --json). Times print in UTC; a node's prompt and config stay in --json.</summary>
internal static class WfReadText
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss'Z'";

    internal static string List(ListWorkflowsResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Workflows.Count == 0)
        {
            return "No workflows yet.\n";
        }

        var text = new StringBuilder();
        foreach (var w in answer.Workflows)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{w.Slug.Value}  {w.Name}  {Active(w.ActiveRevision)}  r{w.LatestRevision} saved by {w.ChangedBy} {Time(w.ChangedAt)}\n");
        }

        return text.ToString();
    }

    internal static string Show(GetWorkflowResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"{answer.Slug.Value}  {answer.Name}\n");
        _ = text.Append(CultureInfo.InvariantCulture, $"showing r{answer.Revision} ({Active(answer.ActiveRevision)}); newest is r{answer.LatestRevision}, saved by {answer.ChangedBy} {Time(answer.ChangedAt)}\n");
        _ = text.Append(CultureInfo.InvariantCulture, $"hash {answer.SpecHash}\n");
        foreach (var node in Items(answer.Spec, "nodes"))
        {
            var title = Word(node, "title");
            _ = text.Append(CultureInfo.InvariantCulture, $"node {Word(node, "id")}  {Word(node, "type")}{(title.Length == 0 ? string.Empty : "  " + title)}\n");
        }

        foreach (var edge in Items(answer.Spec, "edges"))
        {
            var output = Word(edge, "output");
            _ = text.Append(CultureInfo.InvariantCulture, $"edge {Word(edge, "from")} -> {Word(edge, "to")}{(output.Length == 0 ? string.Empty : " (" + output + ")")}\n");
        }

        return text.ToString();
    }

    internal static string Runs(ListWorkflowRunsResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Runs.Count == 0)
        {
            return $"No runs of {answer.Slug.Value} yet.\n";
        }

        var text = new StringBuilder();
        foreach (var r in answer.Runs)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{r.Id.Value}  {r.Status}  {r.Trigger}  {Time(r.StartedAt)}{(r.Error is null ? string.Empty : "  " + r.Error)}\n");
        }

        return text.ToString();
    }

    internal static string Run(GetWorkflowRunResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"{answer.Id.Value}  {answer.Status}  started {Time(answer.StartedAt)}{(answer.EndedAt is { } ended ? ", ended " + Time(ended) : string.Empty)}\n");
        if (answer.Error is not null)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"error: {answer.Error}\n");
        }

        foreach (var node in answer.Nodes)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"node {node.Node.Value}{(node.ItemIndex == 0 ? string.Empty : "[" + node.ItemIndex.ToString(CultureInfo.InvariantCulture) + "]")}  {node.Status}{(node.Truncated ? "  (cut)" : string.Empty)}{(node.Error is null ? string.Empty : "  " + node.Error)}\n");
        }

        return text.ToString();
    }

    internal static string Blocks(ListWorkflowBlocksResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var text = new StringBuilder();
        foreach (var group in answer.Groups)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{group.Group}\n");
            foreach (var block in group.Blocks)
            {
                var fields = string.Join(", ", block.Config.Select(static f => f.Name + (f.Required ? string.Empty : "?")));
                _ = text.Append(CultureInfo.InvariantCulture, $"  {block.Type}  {block.Title}  config: {(fields.Length == 0 ? "none" : fields)}  outputs: {(block.Outputs.Count == 0 ? "none" : string.Join(", ", block.Outputs.Keys))}{(block.Branches is [var only] && only == "out" ? string.Empty : "  branches: " + string.Join(", ", block.Branches))}\n");
            }
        }

        return text.ToString();
    }

    internal static string Docs(SearchWorkflowDocsResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Page is { } page)
        {
            return page.Text.EndsWith('\n') ? page.Text : page.Text + "\n";
        }

        if (answer.Hits.Count == 0)
        {
            return "No page matches; try other words, or run 'sindri wf docs' to list the pages.\n";
        }

        var text = new StringBuilder();
        foreach (var hit in answer.Hits)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{hit.Slug}  {hit.Title}\n");
        }

        return text.ToString();
    }

    private static string Active(int? revision) => revision is { } r ? "active r" + r.ToString(CultureInfo.InvariantCulture) : "off";

    private static IEnumerable<JsonObject> Items(JsonNode spec, string list) => spec[list] is JsonArray items ? items.OfType<JsonObject>() : [];

    private static string Word(JsonObject item, string name) => item[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);
}
