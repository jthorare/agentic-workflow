using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (<see cref="ShellWindow"/>, continued): the edge tab is in the visual tree only while it shows, so a page it doesn't belong to has no tab at all.</summary>
internal sealed partial class ShellWindow
{
    private Panel? _edgeHost;
    private int _edgeIndex;

    private void RememberEdgeTabSlot()
    {
        var tab = this.Get<Button>("EdgeTab");
        if (tab.Parent is Panel host)
        {
            (_edgeHost, _edgeIndex) = (host, host.Children.IndexOf(tab));
        }
    }

    private void SyncEdgeTab()
    {
        if (_edgeHost is not { } host)
        {
            return;
        }

        var tab = this.Get<Button>("EdgeTab");
        var shown = Shell?.IsEdgeTabShown == true;
        if (!shown)
        {
            _ = host.Children.Remove(tab);
        }
        else if (!host.Children.Contains(tab))
        {
            host.Children.Insert(Math.Min(_edgeIndex, host.Children.Count), tab);
        }
    }
}
