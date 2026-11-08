using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>An item's launch block (UI spec E-2, launcher L-15): what the server lets the app offer, from projections only. The app renders it and decides nothing.</summary>
/// <param name="Modes">The modes offered, in order: open, attach, resume. Empty when the item is closed.</param>
/// <param name="DefaultTarget">The agent an Open would use.</param>
/// <param name="ChosenBy">Which rule chose it: lastUsed, profile or builtIn.</param>
/// <param name="Attach">The live session, or null.</param>
/// <param name="Resumable">The newest ended session with an id, once the item was handed back, or null.</param>
/// <param name="InFlight">A launch that is still being started, or null.</param>
/// <param name="LastStarted">The newest launch that started, or null.</param>
/// <param name="Confirm">Set when another Open should ask first, or null.</param>
/// <param name="RepoPath">The item's repo checkout from the approved profile, or null.</param>
/// <param name="Blockers">Why an Open would fail now (from the last probe); empty when it would not, or nothing was probed.</param>
public sealed record ItemLaunch(
    IReadOnlyList<LaunchMode> Modes,
    AgentId DefaultTarget,
    string ChosenBy,
    LaunchAttachTarget? Attach,
    LaunchResumable? Resumable,
    LaunchId? InFlight,
    LaunchLastStarted? LastStarted,
    LaunchConfirm? Confirm,
    string? RepoPath,
    IReadOnlyList<LaunchBlocker> Blockers);
