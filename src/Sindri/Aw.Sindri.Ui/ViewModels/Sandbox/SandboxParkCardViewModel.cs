#pragma warning disable CA1822, MA0041, IDE0060, MA0182, CS0067, CS0649 // why: red stub, replaced by the implementation in the next commit
using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>The park card on a Needs-you row whose Step can't start because of the sandbox (sandbox spec §8).</summary>
internal sealed class SandboxParkCardViewModel : ObservableObject
{
    internal SandboxParkCardViewModel(SandboxBlockerLineView blocker, string step, SandboxCheckViewModel check)
    {
        Check = check;
    }

    public SandboxCheckViewModel Check { get; }

    public string Name { get; } = string.Empty;

    public string Sentence { get; } = string.Empty;

    public SandboxBlockerRow Row { get; } = new(Text: string.Empty, Command: null, IsWait: false);

    public string Severity { get; } = string.Empty;

    public StateGlyph Glyph { get; }

    internal static string FirstClause(string sentence) => throw new NotSupportedException();
}
