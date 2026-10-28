namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>
/// Settings' Sandbox section (sandbox spec §8): a heading that carries the state ("Sandbox", "Sandbox: learning, ends in 6 days", "Sandbox: details
/// unavailable", "Sandbox: status unavailable") and, when loaded, the mechanism, the allowed networks, the limits and learn mode. Read-only: the
/// profile changes them. It follows the Health card, which owns the read.
/// </summary>
internal sealed class SandboxSettingsViewModel : ObservableObject
{
    private readonly SandboxCardViewModel _card;

    /// <summary>The section over the card whose read it shows.</summary>
    /// <param name="card">The Health card.</param>
    internal SandboxSettingsViewModel(SandboxCardViewModel card)
    {
        _card = card;
        card.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SandboxCardViewModel.State), StringComparison.Ordinal))
            {
                Rebuild();
            }
        };
        Rebuild();
    }

    /// <summary>The heading, which carries the state.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The fields when loaded; empty otherwise.</summary>
    public IReadOnlyList<SandboxField> Fields { get; private set; } = [];

    /// <summary>True when there are fields to show.</summary>
    public bool HasFields => Fields.Count > 0;

    /// <summary>True while Sindri doesn't answer: the section is dimmed.</summary>
    public bool IsDimmed => _card.State == SandboxHealthState.ServeDown;

    private static string TitleFor(SandboxHealthState state, IReadOnlyList<SandboxLearnWindow> learning)
    {
        if (state == SandboxHealthState.ServeDown)
        {
            return Labels.SandboxStatusUnavailable;
        }

        if (state == SandboxHealthState.Unreadable)
        {
            return Labels.SandboxDetailsUnavailable;
        }

        return learning.Count > 0 ? Labels.SandboxSettingsLearning(learning[0].Days) : Labels.SandboxHeading;
    }

    private void Rebuild()
    {
        var learning = _card.Learning;
        Title = TitleFor(_card.State, learning);
        Fields = _card.Status is { } status && _card.State is not (SandboxHealthState.ServeDown or SandboxHealthState.Unreadable)
            ?
            [
                new(Labels.SandboxFieldMechanism, Labels.SandboxMechanism(status.Mechanism)),
                new(Labels.SandboxFieldGroups, status.Groups.Count == 0 ? Labels.SandboxNoGroups : string.Join(", ", status.Groups)),
                new(Labels.SandboxFieldLimits, Labels.SandboxLimits(status.Limits.MemoryMax, status.Limits.CpuQuota, status.Limits.TasksMax, status.Limits.Wall)),
                new(Labels.SandboxFieldLearn, learning.Count == 0 ? Labels.SandboxLearnOff : string.Join("; ", learning.Select(l => Labels.SandboxLearnOn(l.Repo, l.Days)))),
                new(Labels.SandboxFieldLogin, status.Login),
            ]
            : [];
        OnChanged(nameof(Title));
        OnChanged(nameof(Fields));
        OnChanged(nameof(HasFields));
        OnChanged(nameof(IsDimmed));
    }
}
