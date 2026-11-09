using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// A recipe command's verb (spec §3.5, §6.3): '[a-z][a-z-]{1,31}', fired with 'sindri workflow command &lt;address&gt;
/// &lt;verb&gt;' as the trigger 'command.&lt;verb&gt;'. The validator refuses the reserved verbs (RCP-TRIG-002).
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CommandVerb>))]
public readonly partial struct CommandVerb : ITypedId<CommandVerb, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A command verb is a lower-case letter, then 1 to 31 lower-case letters or hyphens");

    [GeneratedRegex("^[a-z][a-z-]{1,31}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
