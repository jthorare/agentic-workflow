using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launches;

/// <summary>How an agent's spend was worked out (screens spec E-28).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SpendBasis>))]
public enum SpendBasis
{
    /// <summary>Every model the day's usage names is in the price table: the amount is dollars.</summary>
    [JsonStringEnumMemberName("price-table")]
    PriceTable,

    /// <summary>A model the table lacks (or a reader nobody has verified) was used: tokens are shown and no limit is applied.</summary>
    [JsonStringEnumMemberName("tokens-only")]
    TokensOnly,
}
