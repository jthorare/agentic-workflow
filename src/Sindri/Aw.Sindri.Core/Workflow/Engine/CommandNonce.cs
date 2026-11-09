using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// A command's nonce (spec §5.2): 128 random bits as 32 lower-case hex digits, minted by
/// 'POST /workflow/instances/{a}/nonce', alive for 10 minutes in the API's own table, and the Update's id, so Temporal
/// returns the first outcome for a repeated one.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CommandNonce>))]
public readonly partial struct CommandNonce : ITypedId<CommandNonce, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A nonce is 32 lower-case hex digits");

    [GeneratedRegex("^[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
