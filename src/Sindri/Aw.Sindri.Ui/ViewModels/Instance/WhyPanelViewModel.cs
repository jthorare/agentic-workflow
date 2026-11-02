using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The "Why this model" flyout: what Jev was shown and left out, and the command that explains the pick. It holds no file text.</summary>
internal sealed class WhyPanelViewModel
{
    /// <summary>The keys and ids Jev was shown.</summary>
    public IReadOnlyList<string> Candidates { get; init; } = [];

    /// <summary>The files Jev saw: "src/App.cs · 1200 characters".</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>The files left out: "not shown: big.log, over the size limit".</summary>
    public IReadOnlyList<string> Omitted { get; init; } = [];

    /// <summary>`judge why JD-1`, or empty when Jev was not asked.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>The command's Copy; null when there is no command.</summary>
    public CopyAction? CopyCommand { get; init; }
}
