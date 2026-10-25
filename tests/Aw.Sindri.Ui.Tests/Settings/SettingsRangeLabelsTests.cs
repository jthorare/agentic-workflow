using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>The Text size slider names both ends of its range (ux-pass B-18).</summary>
[Lane(TestLane.Unit)]
public sealed class SettingsRangeLabelsTests
{
    [Fact]
    public void The_slider_names_its_smallest_and_largest_size()
    {
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _ => { });

        Assert.Equal(("90%", "130%"), (settings.MinTextScaleText, settings.MaxTextScaleText));
    }
}
