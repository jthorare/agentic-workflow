using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>An event kind (spec §3.3): '&lt;area&gt;.&lt;name&gt;', for example 'item.routed', registered as an EventDescriptor; at most 64 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<EventKind>))]
public readonly partial struct EventKind : ITypedId<EventKind, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An event kind is '<area>.<name>', for example item.routed, at most 64 characters");

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*\\.[a-z][A-Za-z0-9]*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
