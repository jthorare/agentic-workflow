using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>
/// A fact's key (spec §3.3): 'fact:&lt;name&gt;' with a camelCase name ('fact:stepSpec'), or
/// 'evidence:&lt;kind&gt;@&lt;base|head&gt;' ('evidence:checks@head'). At most 64 characters.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<FactKey>))]
public readonly partial struct FactKey : ITypedId<FactKey, string>
{
    /// <summary>The key of the answer a parked or waiting instance was given ('fact:answer', spec §3.4, §3.8).</summary>
    public static FactKey Answer { get; } = From("fact:answer");

    /// <summary>Whether this is an 'evidence:' key rather than a 'fact:' key.</summary>
    public bool IsEvidence => Value.StartsWith("evidence:", StringComparison.Ordinal);

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value)
            ? Vogen.Validation.Ok
            : Vogen.Validation.Invalid("A fact key is 'fact:<camelCaseName>' or 'evidence:<kind>@<base|head>', at most 64 characters");

    [GeneratedRegex("^(fact:[a-z][A-Za-z0-9]*|evidence:[a-z][a-z0-9]*([.-][a-z0-9]+)*@(base|head))\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
