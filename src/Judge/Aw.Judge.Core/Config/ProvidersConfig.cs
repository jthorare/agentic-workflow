using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Config;

/// <summary>The optional provider selection in config.json.</summary>
/// <param name="AgentClis">Which agent CLIs to use, in priority order (overrides AW_PROVIDER); [] for none; null for the default. Known names only.</param>
/// <param name="Jev">False drops Jev from every chain; null for the default (included).</param>
public sealed record ProvidersConfig(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AgentClis = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Jev = null);
