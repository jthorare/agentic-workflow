using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Work page's Linear view in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3): it reads while Work is the page shown, and its Connect Linear button opens Settings.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Work page's Linear view: its switch, list and since-you-looked strip.</summary>
    public LinearWorkViewModel Linear { get; private set; }

    [MemberNotNull(nameof(Linear))]
    private void InitLinear()
    {
        Linear = new LinearWorkViewModel(_api, _store, _clock);
        Health.Follow(Linear.Refresh);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsWork), StringComparison.Ordinal))
            {
                Linear.SetShown(IsWork);
            }
        };
        Linear.ConnectRequested += (_, _) => GoTo(PageId.Settings);
    }

    // L switches Work between Sindri and Linear from anywhere on the page; C creates an item, and on the Linear view does nothing yet (New issue is not built).
    private bool LinearKeyPressed(string key)
    {
        if (!IsWork)
        {
            return false;
        }

        if (string.Equals(key, "L", StringComparison.Ordinal))
        {
            Linear.Toggle();
            return true;
        }

        return string.Equals(key, "C", StringComparison.Ordinal) && Linear.IsLinear;
    }
}
