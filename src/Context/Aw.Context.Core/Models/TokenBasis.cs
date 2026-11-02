using System.Text.Json.Serialization;

namespace Aw.Context.Core.Models;

/// <summary>Where the token counts of a <see cref="ModelAnswer"/> came from (the ledger's 'tokenBasis').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TokenBasis>))]
public enum TokenBasis
{
    /// <summary>The port counted characters / 4 (the default: no CLI reports usage yet).</summary>
    [JsonStringEnumMemberName("estimated")]
    Estimated,

    /// <summary>The CLI said.</summary>
    [JsonStringEnumMemberName("reported")]
    Reported,
}
