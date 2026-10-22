using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>What the PR gates read from their context: a 'with:' argument, or the instance's fact of the same name when the recipe passed none.</summary>
internal static class PrGateInputs
{
    /// <summary>The worktree the push was made in ('workspace': a path).</summary>
    internal static string? Workspace(GateContext context) => Value(context, "workspace", "fact:workspace") is { ValueKind: JsonValueKind.String } path ? path.GetString() : null;

    /// <summary>The head the forge had when the snapshot ran: the lease of a push ('since', else fact:prState's head.sha).</summary>
    internal static string? SinceHead(GateContext context) => Sha(Value(context, "since", "fact:prState"), "head");

    /// <summary>The PR's base sha at the snapshot (fact:prState's base.sha).</summary>
    internal static string? BaseSha(GateContext context) => Sha(Fact(context, "fact:prState"), "base");

    /// <summary>An argument, else the fact.</summary>
    internal static JsonElement? Value(GateContext context, string argument, string fact) =>
        context.Arguments.TryGetValue(argument, out var value) ? value : Fact(context, fact);

    /// <summary>The instance's fact, or null.</summary>
    internal static JsonElement? Fact(GateContext context, string key) =>
        context.Facts.TryGetValue(FactKey.From(key), out var value) ? value : null;

    private static string? Sha(JsonElement? state, string side) =>
        state is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(side, out var node) && node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("sha", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null;
}
