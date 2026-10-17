using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>A reaction on a thread's opening comment.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="ThreadKey">A key from ListThreads.</param>
/// <param name="Reaction">+1, -1, laugh, confused, heart, hooray, rocket or eyes.</param>
/// <param name="Marker">The idempotency marker (ForgeMarker).</param>
public sealed record ReactionRequest(ForgeRef Pull, string ThreadKey, string Reaction, string Marker);
