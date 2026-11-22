using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Transcripts.Core.Refs;

/// <summary>
/// One turn's opaque ref: its session's ref, '#', and the 1-based line of the file the turn starts on, such as
/// 'transcript:0a1b2c3d#42'. Like <see cref="SessionRef"/>, it names no project, repo or path.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TurnRef>))]
public readonly partial struct TurnRef : ITypedId<TurnRef, string>
{
    private const int SessionLength = 19; // "transcript:" and 8 hex digits

    /// <summary>The session the turn belongs to.</summary>
    public SessionRef Session => SessionRef.From(Value[..SessionLength]);

    /// <summary>The 1-based line of the transcript file the turn starts on.</summary>
    public int Line => int.Parse(Value.AsSpan(SessionLength + 1), NumberStyles.None, CultureInfo.InvariantCulture);

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("TurnRef must be 'transcript:', 8 lowercase hex digits, '#' and a line number from 1");

    [GeneratedRegex("^transcript:[0-9a-f]{8}#[1-9][0-9]{0,8}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
