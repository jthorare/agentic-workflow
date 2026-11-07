using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Work page's Linear view in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3): it reads while Work is the page shown, and its Connect Linear button opens Settings.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Work page's Linear view: its switch, list and since-you-looked strip.</summary>
    public LinearWorkViewModel Linear { get; private set; }

    private static readonly string[] _linearSwallowed = ["C", "Y"];

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
    // The row keys belong to the Linear rows on the Linear view (J / K select, Enter opens, / focuses the filter bar), never to the hidden queue; Y has no action there. In a sheet J, K and Enter go to the sheet.
    private bool LinearKeyPressed(string key, bool inSheet)
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

        if (!Linear.IsLinear)
        {
            return false;
        }

        switch (key)
        {
            case "J" or "K" when !inSheet:
                Linear.Move(key == "J" ? 1 : -1);
                return true;
            case "Enter" when !inSheet:
                _ = Linear.OpenSelected();
                return true;
            case "/" when !inSheet:
                Linear.FocusFilters();
                return true;
            default:
                return string.Equals(key, "/", StringComparison.Ordinal) || _linearSwallowed.Contains(key, StringComparer.Ordinal);
        }
    }
}
