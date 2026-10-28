namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session sheet's Sandbox line and headroom (<see cref="SessionSheetViewModel"/>, continued; SB-6b).</summary>
internal sealed partial class SessionSheetViewModel
{
    /// <summary>"Sandbox: on, writes only to /work/api"; empty for a session with no Sindri launch.</summary>
    public string SandboxLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"memory 2.1 of 12 GiB"; empty when the figure is not served.</summary>
    public string SandboxHeadroom { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the control plane is down: the line is dimmed.</summary>
    public bool IsSandboxDimmed { get; private set => Set(ref field, value); }
}
