#pragma warning disable CA1822, MA0041, IDE0060, MA0182, CS0067, CS0649 // why: red stub, replaced by the implementation in the next commit
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>Check again, its outcome and the resume list (sandbox spec §8).</summary>
internal sealed class SandboxCheckViewModel : ObservableObject
{
    internal SandboxCheckViewModel(ISindriApi api, TimeProvider clock, Func<Uri?> apiUrl, InstanceCommander commands, IClipboard clipboard)
    {
    }

    internal static TimeSpan SendingAfter { get; } = TimeSpan.FromMilliseconds(300);

    public string Label { get; } = string.Empty;

    public SandboxCheckPhase Phase { get; private set; }

    public bool IsChecking => false;

    public LiveLine Live { get; } = new("x");

    public string Result { get; private set; } = string.Empty;

    public bool HasResult => false;

    public IReadOnlyList<SandboxBlockerRow> Blockers { get; private set; } = [];

    public bool ShowsBlockers => false;

    public IReadOnlyList<SandboxResumeRow> ResumeRows { get; private set; } = [];

    public bool ShowsResume => false;

    public string CheckedText { get; private set; } = string.Empty;

    public bool HasChecked => false;

    internal DateTimeOffset? CheckedAt { get; private set; }

    internal event EventHandler? FocusCheckRequested;

    internal event EventHandler? Resolved;

    internal void Seen(DateTimeOffset? checkedAt) => throw new NotSupportedException();

    internal Task Check(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task Resume(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void NotNow() => throw new NotSupportedException();

    internal Task Copy(SandboxBlockerRow row, CancellationToken cancellationToken) => throw new NotSupportedException();
}
