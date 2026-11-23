using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>How `aw-meta delegate` suggests a lane when none is given; every field is optional.</summary>
public sealed record DelegationConfig
{
    /// <summary>Creates a delegation config.</summary>
    /// <param name="lanes">Keywords per lane name: a lane is suggested when one of its keywords is a whole word of the title or the text.</param>
    [JsonConstructor]
    public DelegationConfig(IReadOnlyDictionary<string, IReadOnlyList<string>>? lanes = null)
    {
        Lanes = lanes ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
    }

    /// <summary>Keywords per lane name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Lanes { get; init; }
}
