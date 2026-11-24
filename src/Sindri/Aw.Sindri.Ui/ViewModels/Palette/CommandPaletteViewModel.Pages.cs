using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>The command palette (<see cref="CommandPaletteViewModel"/>, continued): its Sindri and Go to sections' fixed rows.</summary>
internal sealed partial class CommandPaletteViewModel
{
    // The Sindri section's catalog rows, in its order.
    private static readonly string[] _sindri = ["RebuildViews", "StopProcess"];

    // §8's G-chords (and ⌘, for Settings), by page.
    private static readonly (PageId Page, string Key)[] _pages =
    [
        (PageId.Home, "G H"), (PageId.Projects, "G P"), (PageId.Work, "G Q"), (PageId.Context, "G C"), (PageId.Agents, "G A"),
        (PageId.System, "G ."), (PageId.Reviews, "G V"), (PageId.Jobs, "G J"), (PageId.Connections, "G O"), (PageId.Memory, "G M"), (PageId.Workflows, "G W"), (PageId.Resources, "G R"), (PageId.Settings, "⌘,"),
    ];
}
