namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The skill card of the expanded state (skill flows spec §5.2).</summary>
internal sealed partial class ExpandedStateViewModel
{
    /// <summary>The card of a skill block's state; null for any other.</summary>
    public SkillCardViewModel? SkillCard { get; private set { Set(ref field, value); OnChanged(nameof(HasSkillCard)); } }

    /// <summary>True when the state shows a skill card.</summary>
    public bool HasSkillCard => SkillCard is not null;

    /// <summary>Shows (or, with null, removes) the state's skill card: a re-read with the same words keeps the card and does not announce again.</summary>
    /// <param name="input">What the card is drawn from, or null when the state runs no skill block.</param>
    internal void ShowSkillCard(SkillCardInput? input)
    {
        if (input is null)
        {
            SkillCard = null;
            return;
        }

        var card = new SkillCardViewModel(input);
        if (SkillCard is { } current && string.Equals(current.Headline, card.Headline, StringComparison.Ordinal) && current.State == card.State)
        {
            current.Update(input);
            return;
        }

        SkillCard = card;
    }

    /// <summary>Carries what the previous expanded state already said, so a swap for a new view model does not announce the same question again.</summary>
    /// <param name="previous">The expanded state this one replaces.</param>
    internal void CarrySkillCard(ExpandedStateViewModel previous) => _ = (SkillCard, previous.SkillCard);   // stub: the red row fails until this carries the announced question
}
