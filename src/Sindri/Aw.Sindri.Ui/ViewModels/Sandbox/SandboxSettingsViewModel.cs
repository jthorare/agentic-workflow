#pragma warning disable CA1822, MA0041, IDE0060, MA0182, CS0067, CS0649 // why: red stub, replaced by the implementation in the next commit
namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>Settings' Sandbox section (sandbox spec §8): the state line and, when loaded, the mechanism, groups, limits and learn mode.</summary>
internal sealed class SandboxSettingsViewModel : ObservableObject
{
    internal SandboxSettingsViewModel(SandboxCardViewModel card)
    {
    }

    public string Title { get; private set; } = string.Empty;

    public IReadOnlyList<SandboxField> Fields { get; private set; } = [];

    public bool HasFields => false;

    public bool IsDimmed => false;
}
