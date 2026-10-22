using System.Text.Json.Serialization;

namespace Aw.Context.Core.Symbols;

/// <summary>Whether the code index can answer for symbols.</summary>
[JsonConverter(typeof(StrictEnumConverter<SymbolsState>))]
public enum SymbolsState
{
    /// <summary>No code index exists.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,

    /// <summary>The index answers from another commit than the map's or the checkout's head (both commits are named).</summary>
    [JsonStringEnumMemberName("stale")]
    Stale,

    /// <summary>The index answers.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,
}
