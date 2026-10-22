using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One workflow template on the Recipes page (recipes spec §1, §2): its plain name, the id in grey, version, standing, size, needs and produces, and fingerprint. Immutable: a re-read that changes a row replaces it.</summary>
internal sealed class RecipeRowViewModel : ObservableObject
{
    /// <summary>The row over what the list served.</summary>
    /// <param name="served">The list's row.</param>
    internal RecipeRowViewModel(RecipeSummaryView served)
    {
        ArgumentNullException.ThrowIfNull(served);
        Id = served.Id;
        Hash = served.Hash;
        Name = RecipeNames.Of(served.Id, served.DisplayName);
        ShortId = served.Id.StartsWith("workflow.", StringComparison.Ordinal) ? served.Id["workflow.".Length..] : served.Id;
        Standing = StandingOf(served.Standing);
        StandingText = Labels.StandingWord(Standing);
        StaleReasons = [.. Listed(served.Stale).Select(Labels.StaleReason)];
        VersionText = string.Create(CultureInfo.InvariantCulture, $"v{served.Version}");
        StepsText = Labels.StepsWord(Listed(served.States).Count);
        FlowText = Labels.FlowWord(Listed(served.Inputs).Count, Listed(served.Outputs).Count);
        Fingerprint = Labels.Fingerprint(served.Hash);
        AccessibleName = Labels.RecipeRowName(Name, Id, StandingText, IsOutOfDate, served.Version);
        Link = new DeepLink(DeepLinkKind.Recipe, Standing == RecipeStanding.Approved ? Id : Id + "@" + Labels.Hash12(served.Hash));
        (RunsText, FinishedText, TypicalText) = StatsOf(served);
        Signature = SignatureOf(served) + "|" + RunsText + "|" + FinishedText + "|" + TypicalText;
    }

    // The served figures in words: a dash for what is not there (a draft has none), a word of its own for a workflow that only runs inside others.
    private static (string Runs, string Finished, string Typical) StatsOf(RecipeSummaryView served)
    {
        if (served.ChildOnly)
        {
            return (Labels.StatNone, Labels.StatsChildOnly, Labels.StatNone);
        }

        return served.Stats is { } stats
            ? (stats.Runs.ToString(CultureInfo.InvariantCulture), Labels.StatShare(stats.Done, stats.Runs), stats.MedianSeconds is { } seconds ? Labels.Duration(seconds) : Labels.StatNone)
            : (Labels.StatNone, Labels.StatNone, Labels.StatNone);
    }

    private static IReadOnlyList<T> Listed<T>(IReadOnlyList<T>? served) => served ?? [];

    private static RecipeStanding StandingOf(string served) => served switch
    {
        "approved" => RecipeStanding.Approved,
        "revoked" => RecipeStanding.Revoked,
        _ => RecipeStanding.NotApproved,
    };

    private static string SignatureOf(RecipeSummaryView served) =>
        string.Join('|', served.Id, served.Version, served.Hash, served.Standing, served.DisplayName, string.Join(',', Listed(served.Stale)), Listed(served.States).Count, Listed(served.Inputs).Count, Listed(served.Outputs).Count);

    /// <summary>The recipe id.</summary>
    public string Id { get; }

    /// <summary>The recipe's hash as served.</summary>
    internal string Hash { get; }

    /// <summary>The plain name.</summary>
    public string Name { get; }

    /// <summary>The id without "workflow.", grey and mono.</summary>
    public string ShortId { get; }

    /// <summary>The version, "v2".</summary>
    public string VersionText { get; }

    /// <summary>Where it stands.</summary>
    public RecipeStanding Standing { get; }

    /// <summary>The standing word.</summary>
    public string StandingText { get; }

    /// <summary>True when the standing is Approved.</summary>
    public bool IsApproved => Standing == RecipeStanding.Approved;

    /// <summary>The standing as a chip: green when approved, the attention colour when not approved, red when revoked.</summary>
    public StateChip StandingChip => new(StandingText, Standing switch
    {
        RecipeStanding.Approved => ChipTone.Ok,
        RecipeStanding.Revoked => ChipTone.Fail,
        RecipeStanding.NotApproved => ChipTone.Attn,
        _ => throw new InvalidOperationException("Unknown standing"),
    });

    /// <summary>True when something it relies on changed since it was approved (or it is in an old format).</summary>
    public bool IsOutOfDate => StaleReasons.Count > 0;

    /// <summary>The second, grey word.</summary>
    public string OutOfDateText { get; } = Labels.OutOfDate;

    /// <summary>Why it is out of date, in words, one per reason served.</summary>
    public IReadOnlyList<string> StaleReasons { get; }

    /// <summary>True when it is not approved, revoked or out of date: the Needs approval filter shows it.</summary>
    public bool NeedsApproval => Standing != RecipeStanding.Approved || IsOutOfDate;

    /// <summary>The size, "8 steps".</summary>
    public string StepsText { get; }

    /// <summary>What it needs and produces, "2 in, 4 out".</summary>
    public string FlowText { get; }

    /// <summary>The runs in the window, or a dash.</summary>
    public string RunsText { get; }

    /// <summary>The share of runs that finished, or a dash (a word of its own for a workflow that only runs inside others).</summary>
    public string FinishedText { get; }

    /// <summary>The typical run time, or a dash.</summary>
    public string TypicalText { get; }

    /// <summary>The short fingerprint.</summary>
    public string Fingerprint { get; }

    /// <summary>The row's accessible name.</summary>
    public string AccessibleName { get; }

    /// <summary>Opens its sheet: an approved workflow by id, another by its hash (the id alone reads the approved version).</summary>
    public DeepLink Link { get; }

    /// <summary>What a re-read compares to keep this very row: everything it shows.</summary>
    internal string Signature { get; }

    /// <summary>True when selected by the keys.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>Whether the search finds it by name or id.</summary>
    /// <param name="query">The search text; empty matches all.</param>
    /// <returns>True when the name or the id contains the text, ignoring case.</returns>
    internal bool Matches(string query) =>
        query.Length == 0 || Name.Contains(query, StringComparison.OrdinalIgnoreCase) || Id.Contains(query, StringComparison.OrdinalIgnoreCase);
}
