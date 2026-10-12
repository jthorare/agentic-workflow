using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>
/// A block's id (spec §3.3): a kind prefix ('gate', 'step', 'effect', 'evidence' or 'input'), an optional area and a
/// name, kebab-case and dot-separated: 'gate.budget.available', 'input.context.retrieve', or 'step.produce' (§12's
/// Steps have no area). At most 64 characters. The prefix names the block's kind (BlockKinds.Of).
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<BlockId>))]
public readonly partial struct BlockId : ITypedId<BlockId, string>
{
    /// <summary>The kind prefix: 'gate', 'step', 'effect', 'evidence' or 'input'.</summary>
    public string Prefix => Value[..Value.IndexOf('.', StringComparison.Ordinal)];

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value)
            ? Vogen.Validation.Ok
            : Vogen.Validation.Invalid("A block id is '<gate|step|effect|evidence|input>.[<area>.]<name>', kebab-case, at most 64 characters");

    [GeneratedRegex("^(gate|step|effect|evidence|input)(\\.[a-z][a-z0-9]*(-[a-z0-9]+)*){1,2}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
