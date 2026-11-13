using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.CodeIndex;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// Health's Code index card (code-index spec §7.3, CI-10b): one line per repo in scope from GET /sindri/code-index/status, the words keyed by the
/// index's state. Followed like the other views: read on connect and after every event, never folded.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="clock">For the age of an index.</param>
internal sealed partial class CodeIndexCardViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    private const int ShortSha = 7;

    // The layers every line names; any other layer (clones, deps) appears only when it needs a look.
    private static readonly string[] _named = ["structure", "graph", "embeddings"];

    /// <summary>The card's heading.</summary>
    public string Heading { get; } = Labels.CodeIndexCard;

    /// <summary>The lines, one per repo; empty when none is in scope or nothing has been read.</summary>
    public IReadOnlyList<CodeIndexLine> Lines { get; private set => Set(ref field, value); } = [];

    /// <summary>What shows instead of lines: the reading, none-in-scope or unreadable words; empty while lines show.</summary>
    public string Message { get; private set => Set(ref field, value); } = Labels.ReadingCodeIndex;

    /// <summary>True when there is a message to show.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The state in a word at the heading's right: "ok" or "needs a look"; empty with no lines.</summary>
    public string Word { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads the status and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var read = await api.CodeIndexStatus(url, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            Lines = [];
            Word = string.Empty;
            Show(Labels.CodeIndexUnreadable);
            return;
        }

        var repos = read.Value.Repos;
        var health = (read.Value.Health ?? []).ToDictionary(h => h.Repo, StringComparer.Ordinal);
        var views = repos.Select(r => (Repo: r, Health: health.GetValueOrDefault(r.Repo))).ToList();
        Lines = [.. views.Select(v => new CodeIndexLine(Line(v.Repo, v.Health)))];
        Word = repos.Count == 0 ? string.Empty : WordOf(views);
        Show(repos.Count == 0 ? Labels.CodeIndexNone : string.Empty);
    }

    private void Show(string message)
    {
        Message = message;
        OnChanged(nameof(HasMessage));
    }

    // A repo the server serves as 'queued' (no index yet, a build starts within about a minute) is normal work: the card reads "build queued" unless another repo needs a look.
    // A queued repo the server flags needsLook (queued over 30 min, #3479) is a look, not work: it stays in the list the card judges.
    private static string WordOf(List<(CodeIndexRepoView Repo, CodeIndexHealthView? Health)> views)
    {
        var rest = views.Where(static v => !(string.Equals(StateOf(v.Repo, v.Health), "queued", StringComparison.Ordinal) && v.Health?.NeedsLook != true)).ToList();
        if (!rest.TrueForAll(static v => Fine(v.Repo, v.Health)))
        {
            return Labels.CodeIndexNeedsLook;
        }

        return rest.Count < views.Count ? Labels.CodeIndexQueued : Labels.CodeIndexOk;
    }

    private static bool One(string state, params string[] words) => words.Contains(state, StringComparer.Ordinal);

    // stale, building, waiting and queued override the file state; the other health states are the file's own.
    private static string StateOf(CodeIndexRepoView repo, CodeIndexHealthView? health) =>
        health is { } h && One(h.State, "stale", "building", "waiting", "queued") ? h.State : repo.State;

    private static bool Fine(CodeIndexRepoView repo, CodeIndexHealthView? health) =>
        string.Equals(StateOf(repo, health), "ok", StringComparison.Ordinal) && repo.Layers.All(static l => One(l.State, "ok", "disabled", "pending"));

    private string Line(CodeIndexRepoView repo, CodeIndexHealthView? health)
    {
        if (health is { NeedsLook: true, LookWords: { Length: > 0 } lookWords })
        {
            return lookWords;   // the server says why (#3515): a flagged repo never reads as plain "build queued"
        }

        var word = StateWords(StateOf(repo, health), health);
        if (repo.Commit is not { Length: > 0 } commit)
        {
            return repo.Repo + ": " + word;
        }

        var age = repo.BuiltAt is { } built ? ", " + Labels.CodeIndexAge(clock.GetUtcNow() - built) : string.Empty;
        var state = string.Equals(StateOf(repo, health), "ok", StringComparison.Ordinal) ? string.Empty : " (" + word + ")";
        var shown = repo.Layers.Where(Shown).Select(l => Words(l, health)).ToList();
        if (health?.EmbeddingsPercent is { } percent && !repo.Layers.Any(static l => string.Equals(l.Layer, "embeddings", StringComparison.Ordinal)))
        {
            shown.Add(Labels.CodeIndexEmbeddings(percent));
        }

        var layers = string.Join(" · ", shown);
        return repo.Repo + " @ " + commit[..Math.Min(ShortSha, commit.Length)] + age + state + (layers.Length == 0 ? string.Empty : ", " + layers);
    }

    private static bool Shown(CodeIndexLayerView layer) => _named.Contains(layer.Layer, StringComparer.Ordinal) || !One(layer.State, "ok", "disabled");

    // While waiting, the server's own words ("retrying in N min") replace the state's.
    // While paused, no retry or build comes: the line names the pause instead of promising one (#3561).
    private static string StateWords(string state, CodeIndexHealthView? health) => health?.Paused == true && One(state, "missing", "stale", "waiting", "queued")
        ? (One(state, "waiting", "queued") ? string.Empty : Labels.CodeIndexState(state) + " · ") + Labels.CodeIndexPaused
        : string.Equals(state, "waiting", StringComparison.Ordinal) && health?.Waiting is { Length: > 0 } waiting ? waiting : Labels.CodeIndexState(state);

    // The health's percent is the server's figure for the embeddings layer; the layer's own detail is the fallback.
    private static string Words(CodeIndexLayerView layer, CodeIndexHealthView? health) =>
        Labels.CodeIndexLayer(layer.Layer, layer.State, layer.Code, string.Equals(layer.Layer, "embeddings", StringComparison.Ordinal) ? health?.EmbeddingsPercent ?? Percent(layer) : Percent(layer));

    // The embeddings layer says "31 of 100 embedded"; that is the only percentage the status view carries.
    [GeneratedRegex(@"^(?<done>\d+) of (?<all>\d+) embedded$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Embedded { get; }

    private static int? Percent(CodeIndexLayerView layer)
    {
        if (!string.Equals(layer.State, "pending", StringComparison.Ordinal) || Embedded.Match(layer.Detail) is not { Success: true } m
            || !long.TryParse(m.Groups["done"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var done)
            || !long.TryParse(m.Groups["all"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var all) || all <= 0)
        {
            return null;
        }

        return (int)Math.Min(100, done * 100 / all);
    }
}
