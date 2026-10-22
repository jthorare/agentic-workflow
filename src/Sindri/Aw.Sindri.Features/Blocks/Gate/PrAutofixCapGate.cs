using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.pr.autofix-cap' (PR babysitter spec §10.1, IDEA-016): refuses once the CI fixes pushed to this pull request reach
/// the 'cap' (the profile's 'workflow.pr.autofixCap', default 3). The count is the 'ciFixes' counter of 'fact:push'; the
/// gate reads facts only and never the forge.
/// </summary>
internal sealed class PrAutofixCapGate : IGate, IDescribedBlock
{
    private const int DefaultCap = 3;

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrGates.AutofixCap;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var cap = CapOf(context.Arguments);
        var fixes = FixesOf(context.Facts);
        return ValueTask.FromResult(fixes < cap
            ? new GateResult(GateVerdict.Pass, Score: null, Reasons: [])
            : new GateResult(GateVerdict.Fail, Score: null,
                Reasons: [string.Create(CultureInfo.InvariantCulture, $"{fixes} automatic CI fixes were already pushed to this pull request (the cap is {cap}); look at the failing check and answer how to go on")]));
    }

    private static int CapOf(IReadOnlyDictionary<string, JsonElement> arguments)
    {
        if (!arguments.TryGetValue("cap", out var value))
        {
            return DefaultCap;
        }

        var parsed = DefaultCap;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            parsed = number;
        }
        else if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var text))
        {
            parsed = text;
        }

        return parsed < 1 ? DefaultCap : parsed;
    }

    private static int FixesOf(IReadOnlyDictionary<FactKey, JsonElement> facts) =>
        facts.TryGetValue(FactKey.From("fact:push"), out var push)
            && push.ValueKind == JsonValueKind.Object
            && push.TryGetProperty("ciFixes", out var count)
            && count.ValueKind == JsonValueKind.Number
            && count.TryGetInt32(out var fixes)
            ? Math.Max(fixes, 0)
            : 0;
}
