using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The park card of a row whose Step can't start because of the sandbox (<see cref="NeedsYouPageViewModel"/>, continued; sandbox spec 8).</summary>
internal sealed partial class NeedsYouPageViewModel
{
    private (string Row, string Step, string Sentence)? _parkFor;   // the row, step and sentence the card was built for

    /// <summary>Check again, shared with Health's Sandbox card; null where the host has none.</summary>
    internal SandboxCheckViewModel? Sandbox
    {
        get;
        set
        {
            field = value;
            OnChanged(nameof(Park));
        }
    }

    /// <summary>Raised when a pick by the user brings the park card up: focus goes to its heading. A list refresh raises nothing.</summary>
#pragma warning disable CS0067 // why: red stub, raised by the implementation in the next commit
    internal event EventHandler? ParkFocusRequested;
#pragma warning restore CS0067

    /// <summary>The park card for the row under the card, or null when it is not a sandbox park.</summary>
    public SandboxParkCardViewModel? Park
    {
        get
        {
            if (Sandbox is not { } check || Card?.Entry is not { Sandbox: { } blocker } entry)
            {
                field = null;
                _parkFor = null;
                return null;
            }

            var key = (entry.Id, entry.Title, blocker.Text);
            if (field is null || _parkFor != key)
            {
                field = new SandboxParkCardViewModel(blocker, entry.Title, check);
                _parkFor = key;
            }

            return field;
        }
    }
}
