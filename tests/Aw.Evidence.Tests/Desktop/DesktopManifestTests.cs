using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DesktopManifestTests
{
    private const string Head = "\"schema\":\"ui-evidence.desktop-screens/1\",\"designRoot\":\"mockups\"";

    private static string Manifest(string screens, string head = Head) => "{" + head + ",\"screens\":[" + screens + "]}";

    private const string Home = """{"name":"home","design":"home.html","steps":[{"action":"screenshot"}]}""";

    private static Result<DesktopManifest> Read(string json) => DesktopManifestReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void A_manifest_with_a_screen_reads_and_keeps_every_field()
    {
        var read = Read(Manifest("""
            {"name":"workflow-project","design":"workflow-project.html","threshold":12.5,"settleMs":1500,
             "fixture":[["workflows"],["items"]],"sizes":["1440x900"],"accepted":["clock text differs"],
             "masks":[{"x":10,"y":20,"w":30,"h":40,"why":"relative times","size":"1440x900"}],
             "steps":[{"action":"launch"},
               {"action":"click","target":{"automationId":"nav-workflows","index":1},"expectedState":{"state":"visible"},"timeoutMs":4000},
               {"action":"type","target":{"name":"Search"},"value":"sync","replace":true},
               {"action":"key","value":"Enter"},
               {"action":"wait-for","target":{"text":"PRJ-sync"},"expectedState":{"state":"textContains","text":"sync"}},
               {"action":"screenshot"}]}
            """.ReplaceLineEndings("\n")));

        var screen = Assert.Single(read.Value.Screens);
        Assert.Equal(("workflow-project", "workflow-project.html", 12.5, 1500), (screen.Name, screen.Design, screen.Threshold, screen.SettleMs));
        Assert.Equal(["workflows", "items"], screen.Fixture!.Select(static step => string.Join(' ', step)), StringComparer.Ordinal);
        Assert.Equal(new ScreenMask(10, 20, 30, 40, "relative times", "1440x900"), Assert.Single(screen.Masks!));
        Assert.Equal(["clock text differs"], screen.Accepted);
        Assert.Equal(
            [DesktopAction.Launch, DesktopAction.Click, DesktopAction.Type, DesktopAction.Key, DesktopAction.WaitFor, DesktopAction.Screenshot],
            screen.Steps.Select(static s => s.Action));
        var click = screen.Steps[1];
        Assert.Equal((new DesktopTarget(Automation: "nav-workflows", Index: 1), new DesktopExpectedState("visible"), 4000), (click.Target, click.ExpectedState, click.TimeoutMs));
        Assert.Equal(("sync", true), (screen.Steps[2].Value, screen.Steps[2].Replace));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"name":"Home","design":"home.html","steps":[{"action":"screenshot"}]}""", "name: lowercase letters")]
    [InlineData("""{"name":"home","design":"../home.html","steps":[{"action":"screenshot"}]}""", "design: must be an .html file below the design root")]
    [InlineData("""{"name":"home","design":"home.png","steps":[{"action":"screenshot"}]}""", "design: must be an .html file")]
    [InlineData("""{"name":"home","design":"home.html","threshold":101,"steps":[{"action":"screenshot"}]}""", "threshold: must be 0 to 100")]
    [InlineData("""{"name":"home","design":"home.html","sizes":["wide"],"steps":[{"action":"screenshot"}]}""", "'wide' is not a size")]
    [InlineData("""{"name":"home","design":"home.html","masks":[{"x":0,"y":0,"w":0,"h":4,"why":"x"}],"steps":[{"action":"screenshot"}]}""", "a mask needs x, y of 0 or more and w, h of 1 or more")]
    [InlineData("""{"name":"home","design":"home.html","masks":[{"x":0,"y":0,"w":4,"h":4,"why":" "}],"steps":[{"action":"screenshot"}]}""", "a mask needs a 'why'")]
    [InlineData("""{"name":"home","design":"home.html","fixture":[[]],"steps":[{"action":"screenshot"}]}""", "each step must list its arguments")]
    public void A_screen_that_breaks_a_rule_is_refused_with_the_rule_named(string screen, string expected)
    {
        var read = Read(Manifest(screen));

        Assert.Equal(EvidenceErrors.DesktopManifestInvalid.Code, read.Error.Code);
        Assert.Contains(expected, read.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "steps: must list at least one step")]
    [InlineData("""{"action":"click","target":{"automationId":"a"}}""", "must end with exactly one screenshot")]
    [InlineData("""{"action":"screenshot"},{"action":"screenshot"}""", "must end with exactly one screenshot")]
    [InlineData("""{"action":"screenshot"},{"action":"click","target":{"name":"x"}}""", "must end with exactly one screenshot")]
    [InlineData("""{"action":"click","target":{"name":"x"}},{"action":"launch"},{"action":"screenshot"}""", "launch is only the first step")]
    [InlineData("""{"action":"click"},{"action":"screenshot"}""", "steps[0].target: click and wait-for need a target")]
    [InlineData("""{"action":"click","target":{}},{"action":"screenshot"}""", "steps[0].target: name at least one of")]
    [InlineData("""{"action":"click","target":{"index":-1}},{"action":"screenshot"}""", "steps[0].target: name at least one of")]
    [InlineData("""{"action":"click","target":{"name":"--x"}},{"action":"screenshot"}""", "must not be empty or start with '--'")]
    [InlineData("""{"action":"type"},{"action":"screenshot"}""", "steps[0].value: type needs the text to enter")]
    [InlineData("""{"action":"type","value":"--x"},{"action":"screenshot"}""", "steps[0].value: must not be empty or start with '--'")]
    [InlineData("""{"action":"click","target":{"name":"x"},"value":"v"},{"action":"screenshot"}""", "value belongs to type and key, replace to type")]
    [InlineData("""{"action":"key"},{"action":"screenshot"}""", "steps[0].value: key needs the chord to press")]
    [InlineData("""{"action":"click","target":{"name":"x"},"replace":true},{"action":"screenshot"}""", "value belongs to type and key, replace to type")]
    [InlineData("""{"action":"wait-for","target":{"name":"x"}},{"action":"screenshot"}""", "steps[0].expectedState: wait-for needs the state")]
    [InlineData("""{"action":"wait-for","target":{"name":"x"},"expectedState":{"state":"sparkly"}},{"action":"screenshot"}""", "'sparkly' is not one of exists, visible")]
    [InlineData("""{"action":"wait-for","target":{"name":"x"},"expectedState":{"state":"textEquals"}},{"action":"screenshot"}""", "text: given for textEquals and textContains only, and required for them")]
    [InlineData("""{"action":"wait-for","target":{"name":"x"},"expectedState":{"state":"visible","text":"t"}},{"action":"screenshot"}""", "text: given for textEquals and textContains only")]
    [InlineData("""{"action":"click","expectedState":{"state":"visible"}},{"action":"screenshot"}""", "expectedState: needs a target to look at")]
    [InlineData("""{"action":"launch","target":{"name":"x"}},{"action":"screenshot"}""", "launch takes no target, value or expectedState")]
    [InlineData("""{"action":"click","target":{"name":"x"},"timeoutMs":0},{"action":"screenshot"}""", "timeoutMs: must be 1 to 120000")]
    [InlineData("""{"action":"screenshot","timeoutMs":5}""", "screenshot takes no expectedState or timeoutMs")]
    public void A_script_that_breaks_a_rule_is_refused_with_the_rule_named(string steps, string expected)
    {
        var read = Read(Manifest("""{"name":"home","design":"home.html","steps":[""" + steps + "]}"));

        Assert.Equal(EvidenceErrors.DesktopManifestInvalid.Code, read.Error.Code);
        Assert.Contains(expected, read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_screen_without_steps_a_step_with_an_unknown_action_or_key_is_refused()
    {
        Assert.Contains("steps: must list at least one step", Read(Manifest("""{"name":"home","design":"home.html"}""")).Error.Message, StringComparison.Ordinal);
        Assert.Equal(EvidenceErrors.DesktopManifestInvalid.Code, Read(Manifest("""{"name":"home","design":"home.html","steps":[{"action":"hover"}]}""")).Error.Code);
        Assert.Contains("extra", Read(Manifest("""{"name":"home","design":"home.html","steps":[{"action":"screenshot","extra":1}]}""")).Error.Message, StringComparison.Ordinal);
        Assert.Contains("route", Read(Manifest("""{"name":"home","route":"page:Home","design":"home.html","steps":[{"action":"screenshot"}]}""")).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_screens_with_one_name_are_refused()
    {
        var read = Read(Manifest(Home + "," + Home));

        Assert.Contains("the name 'home' is used twice", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_manifest_with_the_wrong_schema_no_screens_or_an_unknown_key_is_refused()
    {
        Assert.Contains("schema: must be ui-evidence.desktop-screens/1", Read(Manifest(Home, "\"schema\":\"x\",\"designRoot\":\".\"")).Error.Message, StringComparison.Ordinal);
        Assert.Contains("screens: must list at least one screen", Read(Manifest(string.Empty)).Error.Message, StringComparison.Ordinal);
        Assert.Contains("extra", Read(Manifest(Home, Head + ",\"extra\":1")).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_design_root_may_be_the_manifests_own_folder_or_another_relative_one_but_never_absolute()
    {
        Assert.True(Read(Manifest(Home, "\"schema\":\"ui-evidence.desktop-screens/1\",\"designRoot\":\".\"")).IsSuccess);
        Assert.Contains("designRoot", Read(Manifest(Home, "\"schema\":\"ui-evidence.desktop-screens/1\",\"designRoot\":\"/x\"")).Error.Message, StringComparison.Ordinal);
    }
}
