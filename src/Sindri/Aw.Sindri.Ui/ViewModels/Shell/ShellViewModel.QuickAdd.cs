using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.ViewModels.Toast;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The quick-add box in the shell (<see cref="ShellViewModel"/>, continued; todo spec §3.2, §3.3): N, Ctrl+Shift+N and the palette entry open it.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The quick-add box.</summary>
    public QuickAddViewModel QuickAdd { get; }

    /// <summary>Raised when N or Ctrl+Shift+N is pressed while Sindri isn't running: the box doesn't open (the toast that says so is another view's).</summary>
    internal event EventHandler? QuickAddWhileStopped;

    // The toasts the box asks for: "Added" with Undo after a save, and the stopped note when it can't open.
    private void WireQuickAddToasts()
    {
        QuickAdd.Added += (_, added) => ShowToast(new ToastMessage(
            added.Due is { } due ? Labels.QuickAddAddedDue(due) : Labels.QuickAddAdded,
            () => UndoAdded(added.Saved)));
        QuickAddWhileStopped += (_, _) => ShowToast(new ToastMessage(Labels.QuickAddStopped));
    }

    // Undo removes the todo just added, at the version Sindri answered with; null when it worked, else why not.
    private async Task<string?> UndoAdded(TodoAcceptedView saved)
    {
        if (Health.ApiUrl() is not { } url)
        {
            return Labels.QuickAddNotAnswering;
        }

        var removed = await _api.RemoveTodo(url, saved.Id, saved.Version, CancellationToken.None).ConfigureAwait(true);
        if (!removed.IsError)
        {
            return null;
        }

        return removed.Error.Code.StartsWith("SND-UI-", StringComparison.Ordinal) ? Labels.QuickAddNotAnswering : removed.Error.Message;
    }

    /// <summary>The N key or Ctrl+Shift+N.</summary>
    /// <returns>True when the key was taken. A form held open (Create, New issue) takes neither key.</returns>
    internal bool PressQuickAdd() => IsChromeVisible && !Create.IsOpen && !NewIssue.IsOpen && OpenQuickAdd();

    /// <summary>Opens the box: the palette and the `?` sheet close first; while Sindri isn't running it raises <see cref="QuickAddWhileStopped"/> instead.</summary>
    /// <returns>True when the key was taken.</returns>
    internal bool OpenQuickAdd()
    {
        if (IsNotRunning)
        {
            OnQuickAddWhileStopped();
            return true;
        }

        Shortcuts.Close();
        Palette.Close();
        QuickAdd.Open();
        return true;
    }

    /// <summary>What happens when the box was asked for while Sindri isn't running.</summary>
    internal void OnQuickAddWhileStopped() => QuickAddWhileStopped?.Invoke(this, EventArgs.Empty);
}
