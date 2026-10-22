using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.pr.base-is-default' (PB spec §10.1, BD4): passes only while the PR's base branch is the repo's default branch. The base is 'base.ref' of the 'pr' argument (else fact:pr), then of
/// fact:prState; the default branch is read in the 'workspace' (argument, else fact:workspace). Anything it can't read fails: a merge is never allowed on a guess.
/// </summary>
/// <param name="defaultBranch">The repo's default branch.</param>
internal sealed class PrBaseIsDefaultGate(IDefaultBranch defaultBranch) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.BaseIsDefault;

    /// <inheritdoc/>
    public async ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (BaseRef(context) is not { } baseRef)
        {
            return Refused("gate.pr.base-is-default can't see the PR's base branch (no base.ref in the PR or its state)");
        }

        if (Workspace(context) is not { } workspace)
        {
            return Refused("gate.pr.base-is-default needs the workspace (a path) to read the repo's default branch");
        }

        var found = await defaultBranch.Of(workspace, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return Refused($"gate.pr.base-is-default can't read the default branch in {workspace}: {found.Error.Message}");
        }

        return string.Equals(baseRef, found.Value, StringComparison.Ordinal)
            ? new GateResult(GateVerdict.Pass, Score: null, Reasons: [])
            : Refused($"the PR's base is '{baseRef}', not the default branch '{found.Value}' (fix: merge or retarget the base PR first)");
    }

    private static string? BaseRef(GateContext context) =>
        RefOf(context.Arguments.TryGetValue("pr", out var argument) ? argument : Fact(context, "fact:pr")) ?? RefOf(Fact(context, "fact:prState"));

    private static string? Workspace(GateContext context) =>
        (context.Arguments.TryGetValue("workspace", out var argument) ? argument : Fact(context, "fact:workspace")) is { ValueKind: JsonValueKind.String } path
            && path.GetString() is { Length: > 0 } text ? text : null;

    private static JsonElement? Fact(GateContext context, string key) => context.Facts.TryGetValue(FactKey.From(key), out var value) ? value : null;

    // A fact reaches a gate as its value, or as the string element of its stored JSON text.
    private static string? RefOf(JsonElement? pr)
    {
        var node = pr;
        if (node is { ValueKind: JsonValueKind.String } text)
        {
            try
            {
                node = JsonDocument.Parse(text.GetString() ?? string.Empty).RootElement;
            }
            catch (JsonException)
            {
                return null;   // not JSON: no base to read
            }
        }

        return node is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("base", out var side) && side.ValueKind == JsonValueKind.Object
            && side.TryGetProperty("ref", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() is { Length: > 0 } value ? value : null;
    }

    private static GateResult Refused(string reason) => new(GateVerdict.Fail, Score: null, [reason]);
}
