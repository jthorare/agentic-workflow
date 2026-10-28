using Aw.Sindri.Ui.Api.Sessions;

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

    private void DrawSandbox(SessionRowView session)
    {
        IsSandboxDimmed = false;
        (SandboxLine, SandboxHeadroom) = session.Sandbox switch
        {
            null => (string.Empty, string.Empty),
            { State: "on" } on => (Labels.SandboxLine(on.Folder, on.Attended), on.Memory is { UsedBytes: { } used, MaxBytes: { } max } ? Labels.SandboxHeadroom(used, max) : string.Empty),
            _ => (Labels.SandboxDetailsUnavailable, string.Empty),
        };
    }

    // why: a failed re-read of a shown sheet means the control plane is down; the line that was there says so rather than keep a stale memory figure.
    private void SandboxServeDown()
    {
        if (SandboxLine.Length > 0)
        {
            (SandboxLine, SandboxHeadroom, IsSandboxDimmed) = (Labels.SandboxStatusUnavailable, string.Empty, true);
        }
    }

    private void ClearSandbox() => (SandboxLine, SandboxHeadroom, IsSandboxDimmed) = (string.Empty, string.Empty, false);
}
