using Aw.Judge.Core.Images;

namespace Aw.Judge.Core.Decisions;

/// <summary>The sizes of a call's images as read and as the deciding provider sent them (Jev after any downscaling); kept with the decision's details so 'judge why' can say what was sent.</summary>
/// <param name="Read">The images as read, in order.</param>
/// <param name="Sent">The images the deciding provider sent, in order; empty when it sent none (an agent CLI reads its own copies, a rule reads nothing).</param>
public sealed record DecisionImages(IReadOnlyList<ImageSize> Read, IReadOnlyList<ImageSize> Sent);
