namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>What the page reads from a run's served status word.</summary>
internal static class RunStatus
{
    /// <summary>True while the run has not ended: running, waiting or queued. Its age counts from the start; an ended run's counts from its end.</summary>
    /// <param name="status">The served status.</param>
    /// <returns>True when the run is live.</returns>
    internal static bool IsLive(string status) =>
        string.Equals(status, "running", StringComparison.Ordinal) || string.Equals(status, "waiting", StringComparison.Ordinal) || string.Equals(status, "queued", StringComparison.Ordinal);

    /// <summary>True while a cancel can be asked for: the run is running or waiting.</summary>
    /// <param name="status">The served status.</param>
    /// <returns>True when Cancel run shows.</returns>
    internal static bool CanCancel(string status) =>
        string.Equals(status, "running", StringComparison.Ordinal) || string.Equals(status, "waiting", StringComparison.Ordinal);

    /// <summary>The look of a status chip. The glyph and the word say it too, so colour is never the only signal.</summary>
    /// <param name="status">The served status.</param>
    /// <returns>Ok for succeeded and running, Attn for waiting, Fail for failed, else Neutral (queued, cancelled, skipped, and a status this build does not know).</returns>
    internal static Api.ChipTone Tone(string status) => status switch
    {
        "succeeded" or "running" => Api.ChipTone.Ok,
        "waiting" => Api.ChipTone.Attn,
        "failed" => Api.ChipTone.Fail,
        _ => Api.ChipTone.Neutral,
    };

    /// <summary>The StateChipView tone word of a chip tone.</summary>
    /// <param name="tone">The chip tone.</param>
    /// <returns>ok, attention or error; neutral for the rest (a running run is ok, a waiting run attention).</returns>
    internal static string ToneWord(Api.ChipTone tone) => tone switch
    {
        Api.ChipTone.Ok => "ok",
        Api.ChipTone.Attn => "attention",
        Api.ChipTone.Fail => "error",
        Api.ChipTone.Neutral => "neutral",
        _ => "neutral",
    };
}
