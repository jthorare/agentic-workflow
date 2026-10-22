using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>A comment's (or its thread's) id on the forge (PR babysitter spec §5.4): printable characters without whitespace, at most 128.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<PrCommentId>))]
public readonly partial struct PrCommentId : ITypedId<PrCommentId, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A comment id is printable characters without whitespace, at most 128");

    [GeneratedRegex("^[^\\s]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 128 ? string.Empty : input;
}
