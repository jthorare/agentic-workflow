namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>POST /sindri/memories's body.</summary>
/// <param name="Name">The new store's name.</param>
/// <param name="Description">What it is for, or null.</param>
internal sealed record CreateMemoryBody(string Name, string? Description);
