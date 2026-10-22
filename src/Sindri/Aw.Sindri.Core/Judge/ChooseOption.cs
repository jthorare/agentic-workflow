namespace Aw.Sindri.Core.Judge;

/// <summary>One choice of a judge call: the outcome's name and when to pick it.</summary>
/// <param name="Name">The outcome name (a recipe's 'choices' entry before its first ': ').</param>
/// <param name="Criterion">When to pick it (the entry after the ': ').</param>
public sealed record ChooseOption(string Name, string Criterion);
