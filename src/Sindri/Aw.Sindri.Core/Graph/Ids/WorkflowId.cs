using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Graph.Ids;

/// <summary>A workflow's id: 'WF-' and 32 lowercase hex digits, a GUIDv7 in 'N' format. The slug is its name for people; the id never changes.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<WorkflowId>))]
public readonly partial struct WorkflowId : ITypedId<WorkflowId, string>
{
    private const string Prefix = "WF-";

    /// <summary>A new workflow id from the id port.</summary>
    /// <param name="ids">The id port (a GUIDv7 in production, a sequence in tests).</param>
    /// <returns>The id.</returns>
    public static WorkflowId New(IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return From(Prefix + ids.NewId().ToString("N", CultureInfo.InvariantCulture));
    }

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A workflow id is 'WF-' and 32 lowercase hex digits");

    [GeneratedRegex("^WF-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
