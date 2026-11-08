using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api;

/// <summary>The tone class of a served chip. A tone this build doesn't know reads as <see cref="Neutral"/> (API rev 8).</summary>
[JsonConverter(typeof(ChipToneJsonConverter))]
internal enum ChipTone
{
    /// <summary>The one attention colour.</summary>
    Attn = 1,

    /// <summary>Something failed.</summary>
    Fail = 2,

    /// <summary>Running well.</summary>
    Ok = 3,

    /// <summary>No colour; also what an unknown tone reads as.</summary>
    Neutral = 4,
}
