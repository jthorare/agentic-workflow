namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One PR on the Reviews list (review page spec §6.1, mockup list-main): title, ref, author, age, CI, required checks and the doc column.</summary>
internal sealed class ReviewRowViewModel
{
    /// <summary>The PR's title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>"acme/api#2071".</summary>
    public string Ref { get; private set; } = string.Empty;

    /// <summary>The author, or "you" for review.login.</summary>
    public string Author { get; private set; } = string.Empty;

    /// <summary>Since it opened: "5 h", "2 d".</summary>
    public string Age { get; private set; } = string.Empty;

    /// <summary>"passing", "failing", "running" or "no CI".</summary>
    public string Ci { get; private set; } = string.Empty;

    /// <summary>The CI state's glyph, never color alone (§6.4).</summary>
    public StateGlyph CiGlyph { get; private set; }

    /// <summary>"2 open", "none open"; empty when the project decides none.</summary>
    public string Required { get; private set; } = string.Empty;

    /// <summary>"claims a doc" or "no doc named".</summary>
    public string Doc { get; private set; } = string.Empty;

    /// <summary>"review requested", "reviewed, 2 new commits"; null in Yours.</summary>
    public string? WaitingChip { get; private set; }

    /// <summary>True for a draft PR (the "draft" chip).</summary>
    public bool IsDraft { get; private set; }

    /// <summary>The row's screen-reader name: title, ref, waiting, author, age, CI, required and doc in one line.</summary>
    public string AutomationName { get; private set; } = string.Empty;
}
