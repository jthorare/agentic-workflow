using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Toast;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Toast;

/// <summary>A toast whose owner turns its Undo off for now (a workflow's Undo while an Activate is on the way): the button and Ctrl+Z do nothing until it is on again.</summary>
[Lane(TestLane.Unit)]
public sealed class ToastUndoAllowedTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly List<string> _undone = [];
    private bool _allowed = true;

    private ToastHostViewModel Shown()
    {
        var host = new ToastHostViewModel(_clock, macOS: false);
        host.Show(new ToastMessage("Step saved.", () =>
        {
            _undone.Add("undo");
            return Task.FromResult<string?>(null);
        }, UndoAllowed: () => _allowed));
        return host;
    }

    [Fact]
    public async Task While_the_owner_says_no_the_key_and_the_button_do_nothing_and_the_toast_stays()
    {
        var host = Shown();
        _allowed = false;

        Assert.False(host.PressUndoKey(inTextField: false));
        Assert.False(await host.Undo());

        Assert.Empty(_undone);
        Assert.Equal((true, true, false), (host.IsShown, host.HasUndo, host.IsUndoEnabled));
    }

    [Fact]
    public async Task When_the_owner_says_yes_again_Undo_runs()
    {
        var host = Shown();
        _allowed = false;
        _clock.Advance(TimeSpan.FromSeconds(1));   // a tick tells the button
        _allowed = true;

        Assert.True(host.IsUndoEnabled);
        Assert.True(await host.Undo());
        _ = Assert.Single(_undone);
    }
}
