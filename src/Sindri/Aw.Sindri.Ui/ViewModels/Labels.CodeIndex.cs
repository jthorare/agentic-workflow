using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Health's Code index card (code-index spec §7.3, CI-10b). The words are keyed by the index's state; none is a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The card's heading.</summary>
    internal const string CodeIndexCard = "Code index";

    /// <summary>A line's accessible name starts with this, then the line.</summary>
    internal const string CodeIndexNamePrefix = "Code index: ";

    /// <summary>The card before its first answer.</summary>
    internal const string ReadingCodeIndex = "Reading the code index…";

    /// <summary>The card when no repo is in scope.</summary>
    internal const string CodeIndexNone = "No repos are indexed yet.";

    /// <summary>The card when the read failed.</summary>
    internal const string CodeIndexUnreadable = "The code index can't be read just now.";

    /// <summary>The card's state word when every repo and layer is fine.</summary>
    internal const string CodeIndexOk = "ok";

    /// <summary>The card's state word when something needs a look.</summary>
    internal const string CodeIndexNeedsLook = "needs a look";

    /// <summary>The card's state word while the only repos not ok are queued for their first build.</summary>
    internal const string CodeIndexQueued = "build queued";

    /// <summary>What an index state reads as when the line has no commit to name (§7.3).</summary>
    /// <param name="state">The served state.</param>
    /// <returns>The words; a state this app doesn't know is shown as served.</returns>
    internal static string CodeIndexState(string state) => state switch
    {
        "ok" => "ok",
        "missing" => "first build queued",
        "damaged" => "damaged, rebuilding on the next check",
        "stale" => "behind main",
        "building" => "building",
        "queued" => CodeIndexQueued,
        "waiting" => "waiting to retry",
        _ => state,
    };

    /// <summary>How old an index is.</summary>
    /// <param name="age">Since it was built.</param>
    /// <returns>"3 min old".</returns>
    internal static string CodeIndexAge(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "under a minute old",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min old"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h old"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays} d old"),
    };

    /// <summary>The embeddings figure on a line whose index has no embeddings layer to carry it.</summary>
    /// <param name="percent">How much is done.</param>
    /// <returns>"embeddings 42%".</returns>
    internal static string CodeIndexEmbeddings(int percent) => string.Create(CultureInfo.InvariantCulture, $"embeddings {percent}%");

    /// <summary>One layer's words: "graph ok", "graph unavailable (SND-CODEIDX-020)", "embeddings pending 31%".</summary>
    /// <param name="layer">The layer's name.</param>
    /// <param name="state">The layer's state.</param>
    /// <param name="code">The code when unavailable.</param>
    /// <param name="percent">How much is done, for a pending layer that knows.</param>
    /// <returns>The words.</returns>
    internal static string CodeIndexLayer(string layer, string state, string? code, int? percent) => state switch
    {
        "disabled" => layer + " off",
        "pending" when percent is { } p => string.Create(CultureInfo.InvariantCulture, $"{layer} pending {p}%"),
        "unavailable" when !string.IsNullOrEmpty(code) => layer + " unavailable (" + code + ")",
        _ => layer + " " + state,
    };
}
