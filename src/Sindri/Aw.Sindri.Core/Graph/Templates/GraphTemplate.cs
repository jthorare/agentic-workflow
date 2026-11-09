namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>A starter workflow an agent or person picks by name ('workflow_create --template').</summary>
/// <param name="Name">The template's name, such as 'weekday-check'.</param>
/// <param name="Description">What it does, in a sentence.</param>
/// <param name="Spec">The graph; it validates in activate mode once its names are filled in.</param>
public sealed record GraphTemplate(string Name, string Description, GraphSpec Spec);
