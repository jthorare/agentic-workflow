namespace Aw.Sindri.Core.Forge;

/// <summary>A pull request found or opened.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="Number">Its number in the repository.</param>
public sealed record OpenedPull(string Link, int Number);
