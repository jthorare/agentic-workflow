namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One entry of the start screen: a template card, or Blank (<see cref="Name"/> empty).</summary>
/// <param name="Letter">"A", "B" or "C" for a template; empty for Blank.</param>
/// <param name="Name">The template's name Sindri takes; empty for Blank.</param>
/// <param name="Title">The card's title.</param>
/// <param name="Description">What it does, in one line.</param>
/// <param name="Trigger">The trigger, in words; empty for Blank.</param>
/// <param name="Steps">The step count and kinds ("3 · trigger, agent, notification"); empty for Blank.</param>
/// <param name="Needs">What it needs ("GitHub connection", or "nothing"); empty for Blank.</param>
/// <param name="LastRan">When it last ran on this box ("never on this box" when it has not); empty for Blank.</param>
internal sealed record StartEntry(string Letter, string Name, string Title, string Description, string Trigger, string Steps, string Needs, string LastRan)
{
    /// <summary>Gets a value indicating whether this is the Blank entry.</summary>
    public bool IsBlank => Name.Length == 0;
}
