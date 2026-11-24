namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The System page's Code index and Context index cards in the shell (<see cref="ShellViewModel"/>, continued; #2775): they read after
/// every event only while System is the page, since nothing off it shows them or the headline they feed, and once when System opens.
/// </summary>
internal sealed partial class ShellViewModel
{
    private void FollowSystemCards()
    {
        Health.FollowCards(() => IsSystem);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsSystem), StringComparison.Ordinal))
            {
                if (IsSystem)
                {
                    Health.ReadCards();
                }
                else
                {
                    Health.StopCards();
                }
            }
        };
    }
}
