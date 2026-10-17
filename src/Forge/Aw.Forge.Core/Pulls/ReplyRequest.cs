using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>A reply to a thread, or a new comment on the conversation.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="ThreadKey">A key from ListThreads, or "pull" for a new top-level comment.</param>
/// <param name="Text">The text; the caller has already redacted it.</param>
/// <param name="Marker">The idempotency marker (ForgeMarker).</param>
public sealed record ReplyRequest(ForgeRef Pull, string ThreadKey, string Text, string Marker);
