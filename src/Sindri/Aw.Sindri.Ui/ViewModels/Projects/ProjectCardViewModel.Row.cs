namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The project as one row of the Projects page (<see cref="ProjectCardViewModel"/>, continued; issue 3717): the served health reason, the PR glyphs, items, agents, who it is blocked on, the target and the age.</summary>
internal sealed partial class ProjectCardViewModel
{
    /// <summary>Sindri's health level as served ("offTrack", "atRisk", "unknown", "onTrack"); null with no open PR that is not a draft.</summary>
    public string? HealthLevel { get; private set => Set(ref field, value); }

    /// <summary>The server's health reason, word for word; empty with no health.</summary>
    public string HealthReason { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row draws a health dot and reason.</summary>
    public bool HasHealth { get; private set => Set(ref field, value); }

    /// <summary>True for Off track: a red dot.</summary>
    public bool IsOffTrack { get; private set => Set(ref field, value); }

    /// <summary>True for At risk: an amber dot.</summary>
    public bool IsAtRisk { get; private set => Set(ref field, value); }

    /// <summary>True for CI not checked: a hollow ring.</summary>
    public bool IsNotChecked { get; private set => Set(ref field, value); }

    /// <summary>True for On track: a green dot.</summary>
    public bool IsOnTrack { get; private set => Set(ref field, value); }

    /// <summary>True for In progress: the state square is the primary colour.</summary>
    public bool IsInProgress { get; private set => Set(ref field, value); }

    /// <summary>True for Paused: the state square is amber.</summary>
    public bool IsPaused { get; private set => Set(ref field, value); }

    /// <summary>The PRs cell's glyphs, in order.</summary>
    public IReadOnlyList<ProjectPrGlyph> Prs { get; private set => Set(ref field, value); } = [];

    /// <summary>"5/12"; empty with no item.</summary>
    public string ItemsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The running agents; empty for none.</summary>
    public string AgentsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"you", a reviewer's login, or empty.</summary>
    public string BlockedOnText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the server says the project or a PR waits on the owner: "you" is drawn bold.</summary>
    public bool IsBlockedOnYou { get; private set => Set(ref field, value); }

    /// <summary>"Dec 7", "Nov 12 passed", or empty (none, or the project is finished).</summary>
    public string TargetText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the target is before today: the cell is red.</summary>
    public bool TargetPassed { get; private set => Set(ref field, value); }

    /// <summary>The last activity's age: "&lt;1m", "2h", "3d", "1w"; empty when unknown.</summary>
    public string AgeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the row is the one the keys act on.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>True when Sindri couldn't read its PR, check and agent facts: the row leaves its health, PRs, items, agents and blocked on cells blank, since the served zeros are not real.</summary>
    public bool FactsMissing
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowsFacts));
            RowName = SpokenRow();
        }
    }

    /// <summary>True when the row draws the truth's cells.</summary>
    public bool ShowsFacts => !FactsMissing;

    /// <summary>The row's accessible name: the name, the reason, the PRs, then the other cells that have words (what the row draws, so no origin).</summary>
    public string RowName { get; private set => Set(ref field, value); } = string.Empty;

    private void DeriveRow()
    {
        var truth = Card.Truth;
        HealthLevel = truth?.Health?.Level;
        HealthReason = truth?.Health?.Reason ?? string.Empty;
        HasHealth = HealthLevel is not null;
        IsOffTrack = string.Equals(HealthLevel, "offTrack", StringComparison.Ordinal);
        IsAtRisk = string.Equals(HealthLevel, "atRisk", StringComparison.Ordinal);
        IsNotChecked = string.Equals(HealthLevel, "unknown", StringComparison.Ordinal);
        IsOnTrack = string.Equals(HealthLevel, "onTrack", StringComparison.Ordinal);
        IsInProgress = string.Equals(Card.State, "inProgress", StringComparison.Ordinal);
        IsPaused = string.Equals(Card.State, "paused", StringComparison.Ordinal);
        Prs = ProjectRows.Glyphs(truth);
        ItemsText = ProjectRows.Items(truth);
        AgentsText = ProjectRows.Agents(truth);
        BlockedOnText = ProjectRows.BlockedOn(truth);
        IsBlockedOnYou = ProjectRows.WaitsOnYou(truth);
        var now = _clock.GetLocalNow();
        (TargetText, TargetPassed) = Card.FinishedAt is null ? ProjectRows.Target(Card.Target, DateOnly.FromDateTime(now.DateTime)) : (string.Empty, false);
        AgeText = Card.LastActivity is { } activity ? Labels.ProjectRowAge(_clock.GetUtcNow() - activity) : string.Empty;
        RowName = SpokenRow();
    }

    private string SpokenRow()
    {
        var parts = new List<string> { Card.Name };   // no origin: the frames draw none on a row

        if (!FactsMissing)
        {
            parts.Add(HealthReason);
            parts.AddRange(Prs.Select(p => p.Name));
            parts.Add(ItemsText);
            parts.Add(BlockedOnText);
        }

        parts.Add(TargetText);
        parts.Add(AgeText);
        return string.Join(", ", parts.Where(p => p.Length > 0));
    }
}
