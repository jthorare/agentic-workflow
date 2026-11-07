using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Skills;

/// <summary>Builds the descriptor 'step.skill.&lt;id&gt;' from a block's manifest entry (skill flows spec §3.2) and names its fact keys (D1).</summary>
public static class SkillDescriptors
{
    private const int MaxFactKey = 64;   // FactKey.cs: at most 64 characters

    /// <summary>The fact key of an output: 'fact:&lt;block id in camelCase&gt;&lt;Output, first letter capitalised&gt;'.</summary>
    /// <param name="blockName">The kebab-case block id.</param>
    /// <param name="output">The output's name.</param>
    /// <returns>The key; an error when it is longer than 64 characters or not a fact key.</returns>
    public static Result<string> FactKeyOf(string blockName, string output)
    {
        ArgumentException.ThrowIfNullOrEmpty(blockName);
        ArgumentException.ThrowIfNullOrEmpty(output);
        var camel = new StringBuilder("fact:");
        var upper = false;
        foreach (var c in blockName)
        {
            if (c == '-')
            {
                upper = true;
                continue;
            }

            _ = camel.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        var key = camel.Append(char.ToUpperInvariant(output[0])).Append(output.AsSpan(1)).ToString();
        if (key.Length > MaxFactKey)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"the fact key '{key}' is {key.Length} characters; a fact key is at most {MaxFactKey} (shorten the block id or the output name)"));
        }

        return FactKey.TryFrom(key, out _) ? key : SindriErrors.InvalidInput.With($"'{key}' is not a fact key (fact:<camelCaseName>)");
    }

    /// <summary>Builds the descriptor: runtime agent, one Text parameter per input, outputs required on the success outcome, the mapped outcomes, and a Version derived from the canonical hash of the entry.</summary>
    /// <param name="manifest">The manifest entry.</param>
    /// <returns>The descriptor.</returns>
    public static Result<BlockDescriptor> Build(SkillBlockManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var success = OutcomeName.From(manifest.Outcomes.Success);
        var outputs = new List<OutputSpec>();
        foreach (var output in manifest.Outputs)
        {
            var key = FactKeyOf(manifest.Name, output.Key);
            if (key.IsError)
            {
                return key.Error;
            }

            outputs.Add(new OutputSpec(FactKey.From(key.Value), [success]));
        }

        var outcomes = manifest.Outcomes.Map.Count == 0 ? [success] : manifest.Outcomes.Map.Values.Distinct(StringComparer.Ordinal).Select(OutcomeName.From).ToList();
        var capabilities = Capability.Tools | Capability.WriteScope | (manifest.Pushes ? Capability.Effects : Capability.None);
        return new BlockDescriptor(
            BlockId.From("step.skill." + manifest.Name), BlockKind.Step, VersionOf(manifest),
            [], outputs, outcomes, capabilities,
            [.. manifest.Inputs.Select(static i => new ParameterSpec(i.Key, ParameterType.Text, i.Required, IsCapabilityField: false))],
            Rung: null, DeskCapable: false, Runtime: BlockRuntime.Agent, MaxDuration: manifest.MaxDuration, Sentence: manifest.Sentence);
    }

    /// <summary>The canonical hash of a manifest entry, args, 'from' and 'values' included; the sentence is not part of it, like every sentence.</summary>
    /// <param name="manifest">The manifest entry.</param>
    /// <returns>The hash in lower-case hex.</returns>
    public static string HashOf(SkillBlockManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var entry = new JsonObject
        {
            ["id"] = manifest.Name,
            ["inputs"] = new JsonObject(manifest.Inputs.Select(static i => KeyValuePair.Create(i.Key, (JsonNode?)new JsonObject
            {
                ["type"] = i.Type.ToString(),
                ["required"] = i.Required,
                ["values"] = i.Values is null ? null : new JsonArray([.. i.Values.Select(static v => (JsonNode?)JsonValue.Create(v))]),
            }))),
            ["args"] = new JsonArray([.. manifest.Args.Select(static a => (JsonNode?)(a.Text is not null ? JsonValue.Create(a.Text) : new JsonObject { ["flag"] = a.Flag, ["input"] = a.Input }))]),
            ["outputs"] = new JsonObject(manifest.Outputs.Select(static o => KeyValuePair.Create(o.Key, (JsonNode?)new JsonObject { ["type"] = o.Type.ToString(), ["from"] = o.From }))),
            ["outcomes"] = new JsonObject
            {
                ["from"] = manifest.Outcomes.From,
                ["map"] = new JsonObject(manifest.Outcomes.Map.Select(static m => KeyValuePair.Create(m.Key, (JsonNode?)JsonValue.Create(m.Value)))),
                ["success"] = manifest.Outcomes.Success,
            },
            ["pushes"] = manifest.Pushes,
            ["maxDuration"] = manifest.MaxDuration.ToString("c", CultureInfo.InvariantCulture),
        };
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Of(entry)));
    }

    // why: the version is a number, derived from the hash so there is no hand-kept field and no edit slips past the pin (D1, S16); never 0.
    private static int VersionOf(SkillBlockManifest manifest) => 1 + (BitConverter.ToInt32(Convert.FromHexString(HashOf(manifest)[..8])) & 0x3fffffff);
}
