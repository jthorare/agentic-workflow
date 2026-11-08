namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Focus should move to a list on Home; the window honours it once Home is drawn.</summary>
/// <param name="target">Which list.</param>
internal sealed class HomeFocusRequestedEventArgs(HomeFocusTarget target) : EventArgs
{
    /// <summary>Which list.</summary>
    public HomeFocusTarget Target { get; } = target;
}
