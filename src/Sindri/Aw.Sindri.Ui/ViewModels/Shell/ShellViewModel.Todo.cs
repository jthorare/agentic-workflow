using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Todo panel in the shell (<see cref="ShellViewModel"/>, continued; todo spec §5): it reads on connect and after every event, first among Home's reads (D12).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Todo panel at the top of Home.</summary>
    public TodoPanelViewModel Todo { get; private set; }

    [MemberNotNull(nameof(Todo))]
    private void InitTodo()
    {
        Todo = new TodoPanelViewModel(_api, _clock);
        Health.Follow(Todo.Refresh);
    }
}
