namespace Aw.Sindri.Core.OpenTargets;

/// <summary>A pull request a row opens.</summary>
/// <param name="Url">Its address.</param>
/// <param name="Number">Its number.</param>
public sealed record NeedsYouOpenPr(string Url, int Number);
