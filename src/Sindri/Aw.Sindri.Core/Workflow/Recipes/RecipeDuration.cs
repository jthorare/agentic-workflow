using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// A recipe duration (spec §3.3): a whole number without leading zeros and 'm', 'h' or 'd' ('10m', '4h', '7d'), so
/// at least one minute; at most six digits. Kept as written, so the long form and the hash keep the author's unit.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecipeDuration>))]
public readonly partial struct RecipeDuration : ITypedId<RecipeDuration, string>
{
    /// <summary>The length of time.</summary>
    public TimeSpan Span
    {
        get
        {
            var n = long.Parse(Value.AsSpan(0, Value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture);
            return Value[^1] switch
            {
                'm' => TimeSpan.FromMinutes(n),
                'h' => TimeSpan.FromHours(n),
                _ => TimeSpan.FromDays(n),
            };
        }
    }

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A duration is a whole number and m, h or d ('10m', '4h', '2d'), at least 1m");

    [GeneratedRegex("^[1-9][0-9]{0,5}[mhd]\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
