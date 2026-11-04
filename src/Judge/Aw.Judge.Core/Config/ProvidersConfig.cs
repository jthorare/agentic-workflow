using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Config;

/// <summary>The optional provider selection in config.json.</summary>
/// <param name="AgentClis">Which agent CLIs to use, in priority order (overrides AW_PROVIDER); [] for none; null for the default. Known names only.</param>
/// <param name="Jev">False drops Jev from every chain; null for the default (included).</param>
/// <param name="InputCapChars">The cap on the state and instructions Jev is sent, in characters (<see cref="MinInputCapChars"/> to <see cref="MaxInputCapChars"/>); null for <see cref="DefaultInputCapChars"/>.</param>
public sealed record ProvidersConfig(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AgentClis = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Jev = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? InputCapChars = null)
{
    /// <summary>
    /// False stops images going to Jev without turning Jev off (it keeps answering text; image calls skip it as 'images-off'); null for the
    /// default, on. AW_JUDGE_JEV_IMAGES=0|1 wins over this.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? JevImages { get; init; }

    /// <summary>
    /// The cap on what the providers are sent when neither the question nor config.json sets one: Jev's state and instructions, and choose's prompt facts. Jev's window is 64k tokens; JSON and code run at
    /// about 3 characters a token, so 64k tokens is ~196,000 characters, and ~10k tokens (~30,000 characters) are left for
    /// the instructions, the criteria and the answer: 160,000 characters of state.
    /// </summary>
    public const int DefaultInputCapChars = 160_000;

    /// <summary>The smallest "providers.inputCapChars" config.json may set.</summary>
    public const int MinInputCapChars = 1_000;

    /// <summary>The largest "providers.inputCapChars" config.json may set.</summary>
    public const int MaxInputCapChars = 256_000;
}
