using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DriverCommandLineTests
{
    private const string Dir = "/tmp/aw-driver-9";
    private const string Frame = "/r/home-1440x900/app.png";

    private static readonly string[] _driver = ["tools/aw-ui-driver", "--quiet"];

    private static IReadOnlyList<string> Only(DesktopStep step) => Assert.Single(DriverCommandLine.Step(_driver, Dir, step, Frame));

    [Fact, Trait("Lane", "unit")]
    public void Ready_waits_thirty_seconds_and_window_sets_the_size_each_after_the_drivers_program_and_directory()
    {
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "ready", "--wait-ms", "30000"], DriverCommandLine.Ready(_driver, Dir));
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "window", "--width", "1440", "--height", "900"], DriverCommandLine.Window(_driver, Dir, new ViewportSize(1440, 900)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_click_names_every_selector_field_it_has_in_the_drivers_flag_order()
    {
        var target = new DesktopTarget(Automation: "save", Name: "Save", Type: "Button", Text: "Save", Contains: "ave", Index: 2);

        var line = Only(new DesktopStep(DesktopAction.Click, target));

        Assert.Equal(
            ["tools/aw-ui-driver", "--quiet", "--dir", Dir, "click", "--automation-id", "save", "--name", "Save", "--type", "Button", "--text", "Save", "--contains", "ave", "--index", "2"],
            line);
    }

    [Fact, Trait("Lane", "unit")]
    public void Typing_gives_the_text_the_selector_when_there_is_one_and_replace_when_asked()
    {
        var appended = Only(new DesktopStep(DesktopAction.Type, new DesktopTarget(Automation: "search"), Value: "agent"));
        var replaced = Only(new DesktopStep(DesktopAction.Type, Value: "x", Replace: true));

        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "type", "--automation-id", "search", "--value", "agent"], appended);
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "type", "--value", "x", "--replace"], replaced);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_key_press_gives_the_chord_and_the_selector_when_there_is_one()
    {
        var focused = Only(new DesktopStep(DesktopAction.Key, Value: "Ctrl+K"));
        var on = Only(new DesktopStep(DesktopAction.Key, new DesktopTarget(Automation: "search"), Value: "Enter"));

        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "key", "--chord", "Ctrl+K"], focused);
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "key", "--automation-id", "search", "--chord", "Enter"], on);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wait_gives_the_state_the_text_and_the_time_limit()
    {
        var line = Only(new DesktopStep(DesktopAction.WaitFor, new DesktopTarget(Text: "Home"), new DesktopExpectedState("textContains", "Hom"), TimeoutMs: 5000));

        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "wait-for", "--text", "Home", "--state", "textContains", "--expect-text", "Hom", "--wait-ms", "5000"], line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_click_with_an_expected_state_is_followed_by_the_wait_for_it()
    {
        var lines = DriverCommandLine.Step(_driver, Dir, new DesktopStep(DesktopAction.Click, new DesktopTarget(Automation: "nav"), new DesktopExpectedState("visible")), Frame);

        Assert.Equal(2, lines.Count);
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "click", "--automation-id", "nav"], lines[0]);
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "wait-for", "--automation-id", "nav", "--state", "visible"], lines[1]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_screenshot_saves_to_the_frame_path_the_whole_window_or_the_element_named_and_a_launch_runs_nothing()
    {
        var window = Only(new DesktopStep(DesktopAction.Screenshot));
        var element = Only(new DesktopStep(DesktopAction.Screenshot, new DesktopTarget(Automation: "panel")));

        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "screenshot", "--out", Frame], window);
        Assert.Equal(["tools/aw-ui-driver", "--quiet", "--dir", Dir, "screenshot", "--automation-id", "panel", "--out", Frame], element);
        Assert.Empty(DriverCommandLine.Step(_driver, Dir, new DesktopStep(DesktopAction.Launch), Frame));
    }
}
