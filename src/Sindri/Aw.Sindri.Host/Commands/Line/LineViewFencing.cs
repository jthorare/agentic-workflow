using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>
/// Fences the line board's text for a session caller (line spec LN-API1, as LN-API2 does for the stop view). A park reason and a phase's next sentence come from models and
/// people, so each is wrapped in the pack fence; the ids, states, links, hashes and numbers are ours and stay plain. A human gets the view as it is.
/// </summary>
internal static class LineViewFencing
{
    private const string Source = "line";

    /// <summary>The view with every piece of model- or person-written text inside the pack fence.</summary>
    internal static LineView Fence(LineView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { Phases = [.. view.Phases.Select(static p => p with { ParkReason = p.ParkReason is null ? null : F(p.ParkReason), Now = F(p.Now) })] };
    }

    private static string F(string text) => text.Length == 0 ? text : UntrustedFence.Wrap(Source, author: null, text);
}
