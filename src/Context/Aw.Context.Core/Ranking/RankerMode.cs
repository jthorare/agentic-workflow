using System.Text.Json.Serialization;

namespace Aw.Context.Core.Ranking;

/// <summary>Which ranker stages a backtest arm runs, whatever the profile says (spec §9.3 'ranker' arms; never reachable from the query API).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RankerMode>))]
public enum RankerMode
{
    /// <summary>Reciprocal rank fusion alone.</summary>
    [JsonStringEnumMemberName("rrf")]
    Rrf,

    /// <summary>Fusion plus the feature scores.</summary>
    [JsonStringEnumMemberName("features")]
    Features,

    /// <summary>Fusion plus features plus the model rerank.</summary>
    [JsonStringEnumMemberName("rerank")]
    Rerank,
}
