using System.Globalization;
using System.Text;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>A small valid recipe text for the real shipped-recipes tests: one produce step, preceded by a wait on each child recipe it uses.</summary>
internal static class SeededRecipeYaml
{
    /// <summary>The recipe text.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="content">Goes into the description, so another value is another hash.</param>
    /// <param name="displayName">The name the app shows.</param>
    /// <param name="children">The recipes it waits on, in order.</param>
    /// <param name="withEvals">Whether it declares evals (approving needs them).</param>
    /// <returns>The text.</returns>
    internal static string Of(string id, int version, string content, string displayName, IReadOnlyList<string> children, bool withEvals)
    {
        var text = new StringBuilder()
            .Append("schema: 1\n")
            .Append(CultureInfo.InvariantCulture, $"id: {id}\n")
            .Append(CultureInfo.InvariantCulture, $"version: {version}\n")
            .Append(CultureInfo.InvariantCulture, $"description: \"Shipped as {content}.\"\n")
            .Append(CultureInfo.InvariantCulture, $"displayName: \"{displayName}\"\n")
            .Append("inputs: [{ key: fact:spec, label: \"Spec\", type: text, kind: text }]\n");
        if (withEvals)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"evals: [evals/{id}/happy.jsonl]\n");
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"initial: {(children.Count == 0 ? "produce" : "wait0")}\nstates:\n");
        for (var i = 0; i < children.Count; i++)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  wait{i}:\n    kind: wait\n    waitFor:\n      child:\n        recipe: {children[i]}\n        inputs: {{ \"fact:spec\": {{ fact: fact:spec }} }}\n        onParked: propagate\n    timeout: {{ after: 2d, then: park }}\n    next: \"Waiting for a recipe it uses.\"\n");
        }

        _ = text.Append("  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the output.\" }\n")
            .Append("  done: { kind: terminal, outcome: resolved }\n");
        if (children.Count > 0)
        {
            _ = text.Append("  dropped: { kind: terminal, outcome: unresolved }\n");
        }

        _ = text.Append("transitions:\n");
        for (var i = 0; i < children.Count; i++)
        {
            var next = i + 1 < children.Count ? "wait" + (i + 1).ToString(CultureInfo.InvariantCulture) : "produce";
            _ = text.Append(CultureInfo.InvariantCulture, $"  - {{ from: wait{i}, to: {next}, on: wait.satisfied, when: {{ outcome: resolved }} }}\n")
                .Append(CultureInfo.InvariantCulture, $"  - {{ from: wait{i}, to: dropped, on: wait.satisfied, when: {{ outcome: cancelled }} }}\n");
        }

        return text.Append("  - { from: produce, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }\n").ToString();
    }
}
