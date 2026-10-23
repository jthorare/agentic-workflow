namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>One captioned group of fields on the session sheet.</summary>
/// <param name="Heading">The group's caption.</param>
/// <param name="Fields">Its fields, in order.</param>
internal sealed record SessionSectionViewModel(string Heading, IReadOnlyList<SessionFieldViewModel> Fields);
