using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Todo panel's other places in the shell (<see cref="ShellViewModel"/>, continued; todo spec §3, §5): the From elsewhere line, G T and the focus requests.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Focus should move to a list on Home (G T, the palette's Todo row, the From elsewhere line's Needs you part).</summary>
    internal event EventHandler<HomeFocusRequestedEventArgs>? HomeFocusRequested;

    /// <summary>The From elsewhere line under the Todo panel's rows.</summary>
    public FromElsewhereViewModel Elsewhere { get; private set; }

    [MemberNotNull(nameof(Elsewhere))]
    private void InitElsewhere()
    {
        Elsewhere = new FromElsewhereViewModel(_api);
        _ = HomeFocusRequested;
    }

    /// <summary>Shows Home and asks for focus on the Todo panel's first row (G T, the palette's Todo row).</summary>
    internal void OpenTodo() => throw new NotSupportedException();
}
