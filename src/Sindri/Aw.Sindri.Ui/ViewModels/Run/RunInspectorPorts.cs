using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>What the inspector may ask of the page: leave for another place, close, and walk to the next or previous step. It has no way to send a command.</summary>
/// <param name="Go">Leaves for another place.</param>
/// <param name="Close">Clears the selection.</param>
/// <param name="Walk">Moves the selection one step; 1 for next, -1 for previous.</param>
internal sealed record RunInspectorPorts(Action<DeepLink> Go, Action Close, Action<int> Walk);
