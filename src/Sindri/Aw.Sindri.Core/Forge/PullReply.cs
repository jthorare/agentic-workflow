namespace Aw.Sindri.Core.Forge;

/// <summary>A reply on a review thread, or on the pull request itself.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="ThreadKey">The thread's key from <see cref="PullThread.Key"/>, or "pull" for a comment on the pull request.</param>
/// <param name="Text">The reply. The forge adapter redacts it before it is sent.</param>
/// <param name="Marker">Makes the reply post once: a second call with the same marker is <see cref="PullDelivery.AlreadyDelivered"/>.</param>
public sealed record PullReply(string Link, string ThreadKey, string Text, string Marker);
