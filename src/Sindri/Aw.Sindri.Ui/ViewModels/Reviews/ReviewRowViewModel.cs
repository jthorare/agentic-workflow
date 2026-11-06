using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One PR on the Reviews list (review page spec §6.1, mockup list-main): title, ref, author, age, CI, required checks and the doc column. A refresh updates the row in place, so focus stays on it.</summary>
internal sealed class ReviewRowViewModel : ObservableObject
{
    internal ReviewRowViewModel(ReviewRowView row, string? login, DateTimeOffset now)
    {
        Ref = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{row.Repo}#{row.Number}");
        (Repo, Number, Href) = (row.Repo, row.Number, row.Href);
        Update(row, login, now);
    }

    /// <summary>The PR's title.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"acme/api#2071"; a row's identity across refreshes.</summary>
    public string Ref { get; }

    /// <summary>The author, or "you" for review.login.</summary>
    public string Author { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Since it opened: "5 h", "2 d".</summary>
    public string Age { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"passing", "failing", "running" or "no CI".</summary>
    public string Ci { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The CI state's glyph, never color alone (§6.4).</summary>
    public StateGlyph CiGlyph { get; private set => Set(ref field, value); }

    /// <summary>"2 open", "none open"; empty when the project decides none.</summary>
    public string Required { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"claims a doc" or "no doc named".</summary>
    public string Doc { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"review requested", "reviewed, 2 new commits"; null in Yours.</summary>
    public string? WaitingChip
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasWaitingChip));
        }
    }

    /// <summary>True for a draft PR (the "draft" chip).</summary>
    public bool IsDraft { get; private set => Set(ref field, value); }

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The draft chip's word.</summary>
    public string DraftText => Labels.ReviewsDraft;
#pragma warning restore CA1822, MA0041

    /// <summary>True when there is a waiting chip to draw.</summary>
    public bool HasWaitingChip => WaitingChip is not null;

    /// <summary>The row's screen-reader name: title, ref, waiting, author, age, CI, required and doc in one line.</summary>
    public string AutomationName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The page model's path ("/reviews/acme/api/2071").</summary>
    internal string Href { get; }

    /// <summary>owner/name.</summary>
    internal string Repo { get; }

    /// <summary>The PR number.</summary>
    internal int Number { get; }

    /// <summary>Shows the PR as last served.</summary>
    /// <param name="row">The served row.</param>
    /// <param name="login">review.login.</param>
    /// <param name="now">The clock.</param>
    internal void Update(ReviewRowView row, string? login, DateTimeOffset now)
    {
        Title = row.Title;
        Author = login is not null && string.Equals(row.Author, login, StringComparison.Ordinal) ? Labels.ReviewsYou : row.Author;
        Age = Labels.ReviewsAge(now - row.OpenedAt);
        (Ci, CiGlyph) = row.Ci switch
        {
            "pass" => (Labels.ReviewsCiPassing, StateGlyph.Ok),
            "fail" => (Labels.ReviewsCiFailing, StateGlyph.Down),
            "running" => (Labels.ReviewsCiRunning, StateGlyph.Stale),
            _ => (Labels.ReviewsCiNone, StateGlyph.None),
        };
        Required = row.RequiredText ?? string.Empty;
        Doc = row.ClaimsDoc ? Labels.ReviewsClaimsDoc : Labels.ReviewsNoDoc;
        WaitingChip = row.WaitingText;
        IsDraft = row.Draft;
        AutomationName = string.Join(
            ", ",
            new[] { Title, Ref, WaitingChip, $"by {Author}", Age, $"CI {Ci}", Required.Length > 0 ? $"required checks {Required}" : null, Doc }.Where(p => p is not null));
    }
}
