using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Transcripts.Core.Refs;

/// <summary>
/// A transcript session's opaque ref: 'transcript:' and 8 lowercase hex digits, the start of the SHA-256 of the provider's
/// wire name and the file's full path (<see cref="TranscriptRefs.ForFile"/>). It names no project, repo or path, and stays
/// the same across runs while the file stays where it is.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SessionRef>))]
public readonly partial struct SessionRef : ITypedId<SessionRef, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("SessionRef must be 'transcript:' and 8 lowercase hex digits");

    [GeneratedRegex("^transcript:[0-9a-f]{8}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
