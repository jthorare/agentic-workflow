using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window's Escape key (<see cref="ShellWindow"/>, continued): what Esc closes, in order.</summary>
internal sealed partial class ShellWindow
{
    // Esc closes the sheet when it shows, or Settings over the Not running card (focus then goes from its hidden control
    // to the page region); otherwise it does nothing here. A sheet hidden while Sindri isn't running stays open, so the
    // keypress isn't spent on what the user can't see and the sheet is back when Sindri is.
    private bool Escape()
    {
        if (Shell is not { } shell)
        {
            return false;
        }

        if (EscapeOverlay(shell))
        {
            return true;
        }

        if (shell.NewAgent.IsOpen)
        {
            shell.NewAgent.Close();   // keeps what was typed (agents spec §15.4)
            return true;
        }

        if (EscapeItemLayer(shell))
        {
            return true;
        }

        if (EscapeReviewPage(shell))
        {
            return true;
        }

        if (EscapeTodoPanel(shell))
        {
            return true;
        }

        if (EscapeSheetOrInbox(shell))
        {
            return true;
        }

        if (!shell.LeaveSettings())
        {
            return false;
        }

        Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        return true;
    }

    // The palette, then the ? sheet, then the quick-add box, then the Todo panel's link list (todo spec §3.3).
    private static bool EscapeOverlay(ShellViewModel shell)
    {
        if (shell.Palette.IsOpen)
        {
            shell.Palette.Close();
        }
        else if (shell.Shortcuts.IsOpen)
        {
            shell.Shortcuts.Close();
        }
        else if (shell.QuickAdd.IsOpen)
        {
            shell.QuickAdd.Close();
        }
        else if (shell.IsHomeShown && shell.Todo.LinkList.Close())
        {
            return true;   // the Todo panel's link list: the innermost layer there is (todo spec §3.3)
        }
        else
        {
            return false;
        }

        return true;
    }

    // Esc leaves a review page for the Reviews list.
    private static bool EscapeReviewPage(ShellViewModel shell)
    {
        if (shell.IsReviewPageShown && !shell.IsSheetOpen)
        {
            shell.CloseReviewPage();   // Esc leaves a review page for the Reviews list
            return true;
        }

        return false;
    }
}
