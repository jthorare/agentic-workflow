namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>One network learn window that is open (wire contract item 4).</summary>
/// <param name="Repo">The repo the window is for; null for every repo.</param>
/// <param name="EndsAt">When the window ends.</param>
internal sealed record SandboxLearnView(string? Repo, DateTimeOffset EndsAt);
