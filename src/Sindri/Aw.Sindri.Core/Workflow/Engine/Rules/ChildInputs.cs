using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The id and the input facts of a child a 'waitFor: { child }' creates (spec §5.1, §5.8).</summary>
internal static class ChildInputs
{
    /// <summary>
    /// The child's opaque id (spec §5.1): a UUIDv8 over SHA-256 of its address, which is its workflow id, so the same
    /// child always has the same id and a grandchild, at an address of its own, has an id of its own.
    /// </summary>
    /// <param name="address">The child's address.</param>
    /// <returns>The child's id.</returns>
    public static WorkflowInstanceId IdFor(InstanceAddress address) => WorkflowInstanceId.Of(address);

    /// <summary>The child's own pins: the hash of each recipe its child waits name, as the pinned resolver has it; one it lacks is left out, so creating that grandchild is SND-RECIPE-002.</summary>
    /// <param name="recipe">The child recipe.</param>
    /// <param name="pinned">The pinned child recipes.</param>
    /// <returns>The pins by recipe id.</returns>
    public static IReadOnlyDictionary<RecipeId, RecipeHash> Pins(Recipe recipe, IChildRecipeResolver pinned)
    {
        var pins = new Dictionary<RecipeId, RecipeHash>();
        foreach (var id in RecipeChildren.Of(recipe))
        {
            if (pinned.Resolve(id) is { } grandchild)
            {
                pins[id] = grandchild.Hash;
            }
        }

        return pins;
    }

    /// <summary>
    /// The child's inputs: a '{ fact }' input copies the parent's fact, a literal is recorded by its JSON, and a
    /// '{ profile }' input is left to the child, which reads the profile itself.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="wait">The child wait.</param>
    /// <returns>The input facts.</returns>
    public static IReadOnlyDictionary<FactKey, FactValue> From(WorkflowInstance parent, ChildWait wait)
    {
        var inputs = new Dictionary<FactKey, FactValue>();
        foreach (var (key, value) in wait.Inputs)
        {
            if (value.Fact is { } fact && parent.Facts.TryGetValue(fact, out var held))
            {
                inputs[key] = held;
            }
            else if (value.Literal is { } literal)
            {
                inputs[key] = Literal(literal);
            }
        }

        return inputs;
    }

    // A literal has no blob, so its ref carries the compact JSON itself: 'literal:{json}', with the SHA-256 of that JSON.
    // This ref convention is accepted for now (P3 coordinator, unit (d)); it isn't in the spec yet.
    private static FactValue Literal(JsonElement literal)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            literal.WriteTo(writer);
        }

        return new FactValue("literal:" + Encoding.UTF8.GetString(buffer.WrittenSpan), Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan)), HeadSha: null);
    }
}
