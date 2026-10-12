using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>
/// A recipe's hash (spec §3.1): 'sha256:' and 64 lower-case hex digits of SHA-256 over the RFC 8785 canonical JSON
/// of the normalized long form, without 'hash'. Shown as 12 hex digits (<see cref="Display"/>); input may name it by
/// a prefix of 8 or more (<see cref="MatchesPrefix"/>).
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecipeHash>))]
public readonly partial struct RecipeHash : ITypedId<RecipeHash, string>
{
    private const string Scheme = "sha256:";

    /// <summary>The 64 hex digits, without 'sha256:'.</summary>
    public string Digest => Value[Scheme.Length..];

    /// <summary>The first 12 hex digits, as listings and the 'id@hash12' form show it.</summary>
    public string Display => Digest[..12];

    /// <summary>The hash of <paramref name="canonicalJson"/>.</summary>
    /// <param name="canonicalJson">The RFC 8785 canonical JSON of the normalized recipe without 'hash', UTF-8.</param>
    /// <returns>The hash.</returns>
    public static RecipeHash Of(ReadOnlySpan<byte> canonicalJson) =>
        From(Scheme + Convert.ToHexStringLower(SHA256.HashData(canonicalJson)));

    /// <summary>Whether <paramref name="text"/> names a hash by prefix: 8 to 64 lower-case hex digits, with or without 'sha256:'.</summary>
    /// <param name="text">What the user typed.</param>
    /// <returns>True when it is a well-formed prefix.</returns>
    public static bool IsPrefix(string text) => text is not null && PrefixShape.IsMatch(text);

    /// <summary>Whether this hash starts with <paramref name="prefix"/> (a well-formed prefix, <see cref="IsPrefix"/>).</summary>
    /// <param name="prefix">8 to 64 hex digits, with or without 'sha256:'.</param>
    /// <returns>True when the prefix is well formed and this hash starts with it.</returns>
    public bool MatchesPrefix(string prefix) =>
        IsPrefix(prefix) && Digest.StartsWith(prefix.StartsWith(Scheme, StringComparison.Ordinal) ? prefix[Scheme.Length..] : prefix, StringComparison.Ordinal);

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A recipe hash is 'sha256:' and 64 lower-case hex digits");

    [GeneratedRegex("^sha256:[0-9a-f]{64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    [GeneratedRegex("^(sha256:)?[0-9a-f]{8,64}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrefixShape { get; }
}
