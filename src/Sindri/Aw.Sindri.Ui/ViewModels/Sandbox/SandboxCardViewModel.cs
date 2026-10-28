#pragma warning disable CA1822, MA0041, IDE0060, MA0182, CS0067, CS0649 // why: red stub, replaced by the implementation in the next commit
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>Health's Sandbox row and Settings' data (sandbox spec §8): read from GET /sindri/sandbox/status.</summary>
internal sealed class SandboxCardViewModel : ObservableObject
{
    internal SandboxCardViewModel(ISindriApi api, TimeProvider clock, Func<Uri?> apiUrl, InstanceCommander commands, IClipboard clipboard)
    {
        Check = new SandboxCheckViewModel(api, clock, apiUrl, commands, clipboard);
    }

    public string Heading { get; } = Labels.SandboxHeading;

    public SandboxCheckViewModel Check { get; }

    public SandboxHealthState State { get; private set; }

    public string RowText { get; private set; } = string.Empty;

    public string StateWord { get; private set; } = string.Empty;

    public bool IsDimmed => false;

    public bool IsWarning => false;

    public StateGlyph Glyph { get; private set; }

    public IReadOnlyList<SandboxBlockerRow> Blockers { get; private set; } = [];

    public bool ShowsCheck => false;

    internal SandboxStatusView? Status { get; private set; }

    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();
}
