namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>One open learn window, as the owner reads it.</summary>
/// <param name="Repo">The repo it is for; null for every repo.</param>
/// <param name="Days">Whole days left, at least 1.</param>
internal sealed record SandboxLearnWindow(string? Repo, int Days);
