using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Engine;

/// <summary>A named page's id (pages spec §3.1): the slug of its name ('schedule-week' for Schedule week), unique among the pages.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<PageId>))]
public readonly partial struct PageId : ITypedId<PageId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An id is 1 to 64 lower-case letters, digits, '.' or '-', starting with a letter or digit (like 'schedule-week')");

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
