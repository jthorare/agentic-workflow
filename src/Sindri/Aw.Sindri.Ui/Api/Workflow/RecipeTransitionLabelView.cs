namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The plain-word label of an edge of the recipe (the engine's 'transitionLabels'), for the run canvas's pills.</summary>
/// <param name="From">The states the edge can leave.</param>
/// <param name="To">The state it arrives at.</param>
/// <param name="On">The outcome or trigger it leaves on.</param>
/// <param name="Label">The label in words.</param>
internal sealed record RecipeTransitionLabelView(IReadOnlyList<string> From, string To, string On, string Label);
