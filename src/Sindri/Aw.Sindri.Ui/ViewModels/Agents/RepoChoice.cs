namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A repo of the profile, as the form lists it.</summary>
/// <param name="Name">The repo's name.</param>
/// <param name="Onboarded">False draws "not onboarded"; the repo can still be started in.</param>
internal sealed record RepoChoice(string Name, bool Onboarded);
