using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// A Step run's token (spec §5.1, §5.3): '{entry}.{n}', the frame entry and the run's number within it. A public
/// counter, never a credential; a report with any other token than the open run's is stale (SND-WF-005).
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RunToken>))]
public readonly partial struct RunToken : ITypedId<RunToken, string>
{
    /// <summary>The frame entry the run belongs to.</summary>
    public int Entry => int.Parse(Value.AsSpan(0, Value.IndexOf('.', StringComparison.Ordinal)), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The run's number within its entry, from 1; a re-run in the same entry takes the next.</summary>
    public int Number => int.Parse(Value.AsSpan(Value.IndexOf('.', StringComparison.Ordinal) + 1), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The token '{entry}.{number}'.</summary>
    /// <param name="entry">The frame entry, from 1.</param>
    /// <param name="number">The run's number within it, from 1.</param>
    /// <returns>The token.</returns>
    public static RunToken For(int entry, int number) =>
        From(string.Create(CultureInfo.InvariantCulture, $"{entry}.{number}"));

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A run token is '{entry}.{n}', two whole numbers from 1");

    [GeneratedRegex("^[1-9][0-9]{0,8}\\.[1-9][0-9]{0,8}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
