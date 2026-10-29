using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Sandbox card's follow and its park card on the Needs-you page (<see cref="ShellViewModel"/>, continued; sandbox spec 8).</summary>
internal sealed partial class ShellViewModel
{
    // The status is read on connect and after every event with the other views; the park card on the inbox shares Health's Check again.
    private NeedsYouPageViewModel InitSandbox(NeedsYouPageViewModel inbox)
    {
        Health.Follow(Health.Sandbox.Refresh);
        inbox.Sandbox = Health.Sandbox.Check;
        return inbox;
    }
}
