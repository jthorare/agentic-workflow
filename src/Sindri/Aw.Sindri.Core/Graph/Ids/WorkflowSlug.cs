using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Graph.Ids;

/// <summary>A workflow's name in commands and branch names ('wf/&lt;slug&gt;/&lt;run&gt;'): lowercase letters, digits and '-', starting with a letter or digit, at most 63.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<WorkflowSlug>))]
public readonly partial struct WorkflowSlug : ITypedId<WorkflowSlug, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A workflow slug is lowercase letters, digits and '-', starting with a letter or digit, at most 63");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
