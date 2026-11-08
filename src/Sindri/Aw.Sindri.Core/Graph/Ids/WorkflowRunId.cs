using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Graph.Ids;

/// <summary>A run's id, '&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;' ('contract-drift@3:12'): the 12th run of revision 3. Not a branch or session name (it holds '@' and ':'); the launch id keys those.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<WorkflowRunId>))]
public readonly partial struct WorkflowRunId : ITypedId<WorkflowRunId, string>
{
    /// <summary>The run id of one occurrence of a revision.</summary>
    /// <param name="slug">The workflow.</param>
    /// <param name="revision">The revision the run uses, 1 or more.</param>
    /// <param name="occurrence">The run's number among that revision's runs, 1 or more.</param>
    /// <returns>The id.</returns>
    public static WorkflowRunId Of(WorkflowSlug slug, int revision, int occurrence) =>
        From(string.Create(CultureInfo.InvariantCulture, $"{slug.Value}@{revision}:{occurrence}"));

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A run id is '<slug>@<revision>:<occurrence>', both numbers 1 or more");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}@[1-9][0-9]{0,8}:[1-9][0-9]{0,8}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
