namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The skill card of the expanded state (skill flows spec §5.2). Red: the members throw until the card is wired in.</summary>
internal sealed partial class ExpandedStateViewModel
{
#pragma warning disable CA1822, MA0041 // red stubs: instance reads in the green commit
    /// <summary>The card of a `step.skill.*` state; null for any other.</summary>
    public SkillCardViewModel? SkillCard => throw new NotSupportedException();

    /// <summary>True when the state shows a skill card.</summary>
    public bool HasSkillCard => throw new NotSupportedException();
#pragma warning restore CA1822, MA0041

    /// <summary>Shows (or, with null, removes) the state's skill card: a re-read with the same words keeps the card and does not announce again.</summary>
    /// <param name="input">What the card is drawn from, or null when the state runs no skill block.</param>
    internal void ShowSkillCard(SkillCardInput? input) => throw new NotSupportedException();
}
