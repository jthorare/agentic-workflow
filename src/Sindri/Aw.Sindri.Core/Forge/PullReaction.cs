namespace Aw.Sindri.Core.Forge;

/// <summary>A reaction on a review thread.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="ThreadKey">The thread's key from <see cref="PullThread.Key"/>.</param>
/// <param name="Reaction">The reaction, such as "+1".</param>
/// <param name="Marker">Makes the reaction post once.</param>
public sealed record PullReaction(string Link, string ThreadKey, string Reaction, string Marker);
