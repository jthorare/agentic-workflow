using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

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
        var facts = Elements(run.Facts);
        var arguments = Arguments(command.With, run.Facts);
        return new EffectContext(command.Effect, command.Identity.Text, run.CommandId, command.Origin == CommandOrigin.Engine,
            run.WorkflowId, run.Item, run.Attempt, arguments, facts, run.Text);
    }

    /// <summary>The facts as JSON, each the stored reference.</summary>
    /// <param name="facts">The instance's facts.</param>
    /// <returns>The facts by key.</returns>
    public static Dictionary<FactKey, JsonElement> Elements(IReadOnlyDictionary<FactKey, FactValue> facts) =>
        facts.ToDictionary(static f => f.Key, static f => Element(f.Value));

    /// <summary>The 'with:' arguments resolved to values: a literal as written, a '{ fact }' as the stored fact; a '{ profile }' is left out.</summary>
    /// <param name="with">The arguments as the recipe wrote them.</param>
    /// <param name="facts">The instance's facts.</param>
    /// <returns>The values by parameter name.</returns>
    public static Dictionary<string, JsonElement> Arguments(IReadOnlyDictionary<string, WithValue> with, IReadOnlyDictionary<FactKey, FactValue> facts)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in with)
        {
            if (value.Literal is { } literal)
            {
                arguments[name] = literal;
            }
            else if (value.Fact is { } fact && facts.TryGetValue(fact, out var stored))
            {
                arguments[name] = Element(stored);
            }
        }

        return arguments;
    }

    /// <summary>
    /// A Step input's 'with:' arguments: those of <see cref="Arguments"/>, plus each '{ profile: key }' as the reference '{"profile": key}'. The input resolves it
    /// against the approved profile inside its fill (ContextInputFacts), never here: the workflow only carries the key, so a replay reads the same history.
    /// </summary>
    /// <param name="with">The arguments as the recipe wrote them.</param>
    /// <param name="facts">The instance's facts.</param>
    /// <returns>The values by parameter name.</returns>
    public static Dictionary<string, JsonElement> InputArguments(IReadOnlyDictionary<string, WithValue> with, IReadOnlyDictionary<FactKey, FactValue> facts)
    {
        var arguments = Arguments(with, facts);
        foreach (var (name, value) in with)
        {
            if (value.Profile is { } key)
            {
                arguments[name] = ProfileReference(key);
            }
        }

        return arguments;
    }

    private static JsonElement ProfileReference(string key)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("profile", key);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static JsonElement Element(FactValue value)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(value, _json));
        return document.RootElement.Clone();
    }
}
