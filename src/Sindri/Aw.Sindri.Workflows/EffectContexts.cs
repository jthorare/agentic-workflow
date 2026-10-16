using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What a block's runtime sees of an effect (spec §2, §5.7; engine P6 unit (a)): its id, identity, command id and origin,
/// the instance, and its 'with:' arguments resolved to values: a literal as written, a '{ fact }' as the instance's
/// stored fact. A '{ profile }' argument is left out: no shipped effect takes one (RCP-CAP-007 keeps them to capability
/// parameters).
/// </summary>
internal static class EffectContexts
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The context <paramref name="run"/> dispatches with.</summary>
    /// <param name="run">The effect and its emission.</param>
    /// <returns>The context.</returns>
    public static EffectContext Of(EffectRun run)
    {
        var command = run.Command;
        var facts = run.Facts.ToDictionary(static f => f.Key, static f => Element(f.Value));
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in command.With)
        {
            if (value.Literal is { } literal)
            {
                arguments[name] = literal;
            }
            else if (value.Fact is { } fact && facts.TryGetValue(fact, out var stored))
            {
                arguments[name] = stored;
            }
        }

        return new EffectContext(command.Effect, command.Identity.Text, run.CommandId, command.Origin == CommandOrigin.Engine,
            run.WorkflowId, run.Item, run.Attempt, arguments, facts, run.Text);
    }

    private static JsonElement Element(FactValue value)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(value, _json));
        return document.RootElement.Clone();
    }
}
