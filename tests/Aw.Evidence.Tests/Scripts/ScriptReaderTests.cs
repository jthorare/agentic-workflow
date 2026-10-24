using Aw.Evidence.Core;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Tests.Scripts;

public sealed class ScriptReaderTests
{
    private const string Valid =
        """{"route":"/hospitals","role":"manager","viewports":["desktop","phone"],"steps":[""" +
        """{"action":"goto","target":"/hospitals","expectedState":{"kind":"url-path","path":"/hospitals"}},""" +
        """{"action":"fill","target":"search","value":"St","expectedState":{"kind":"input-value","testId":"search","value":"St"}},""" +
        """{"action":"click","target":"open-btn","expectedState":{"kind":"testid-visible","testId":"panel"}},""" +
        """{"action":"expect-visible","target":"Hospitals","expectedState":{"kind":"text-visible","text":"Hospitals"}},""" +
        """{"action":"click","target":"close","expectedState":{"kind":"text-absent","text":"Panel"}}]}""";

    private static Kernel.Result<UiScript> Read(string json) => ScriptReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void A_well_formed_script_reads_into_typed_steps_and_expectations()
    {
        var script = Read(Valid).Value;

        Assert.Equal("manager", script.Role);
        Assert.Equal([ViewportKind.Desktop, ViewportKind.Phone], script.Viewports);
        Assert.Equal(StepAction.ExpectVisible, script.Steps[3].Action);
        Assert.Equal("St", Assert.IsType<InputValue>(script.Steps[1].ExpectedState).Value);
        Assert.Equal("panel", Assert.IsType<TestIdVisible>(script.Steps[2].ExpectedState).TestId.Value);
        _ = Assert.IsType<TextAbsent>(script.Steps[4].ExpectedState);
        Assert.Null(script.Planning);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[]}""", "at least one step")]
    [InlineData("""{"route":"/","role":"Intern","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""", "role must be a lower-case kebab-case name")]
    [InlineData("""{"route":"/","role":"","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""", "role must be a lower-case kebab-case name")]
    [InlineData("""{"route":"/","role":"manager","viewports":[],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""", "at least one viewport")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"text-visible","text":""}}]}""", "text-visible needs a text")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}],"planning":{"inputTokens":-1}}""", "must not be negative")]
    public void A_script_breaking_a_rule_is_refused_with_the_rule(string json, string rule)
    {
        var result = Read(json);

        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, result.Error.Code);
        Assert.Contains(rule, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"route":"/","role":"super admin","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""")]
    [InlineData("""{"route":"/","role":"manager","viewports":["tablet"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"hover","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"looks-nice"}}]}""")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"text":"no kind"}}]}""")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/"}]}""")]
    [InlineData("""{"route":"/","role":1,"viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""")]
    [InlineData("""{"route":"/","role":7,"viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}]}""")]
    [InlineData("not json")]
    public void A_malformed_script_is_one_clear_script_error(string json) =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(json).Error.Code);

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/"}]}""", "$.steps[0]: missing required property \"expectedState\"")]
    [InlineData("""{"route":"/","role":7,"viewports":["desktop"],"steps":[]}""", "$.role: 7 is not a valid value")]
    [InlineData("""{"route":null,"role":"manager","viewports":["desktop"],"steps":[]}""", "$.route: must not be null")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path"}}]}""", "$.steps[0].expectedState: missing required property \"path\"")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"path":"/"}}]}""", "$.steps[0].expectedState: missing required property \"kind\"")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"looks-nice"}}]}""", "$.steps[0].expectedState.kind: \"looks-nice\" is not a known kind (text-visible, text-absent, testid-visible, testid-absent, url-path, url-search, input-value)")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"testid-visible","testId":""}}]}""", "$.steps[0].expectedState.testId: \"\" is not a valid value")]
    [InlineData("not json", "$: not valid JSON (line 1, column 2)")]
    public void A_malformed_script_is_described_by_its_json_location_in_its_own_terms(string json, string message)
    {
        var error = Read(json).Error;

        Assert.Equal((EvidenceErrors.ScriptInvalid.Code, message), (error.Code, error.Message));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"fill","target":"search","vaule":"St","expectedState":{"kind":"input-value","testId":"search","value":"St"}}]}""", "$.steps[0].vaule")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}],"viewport":"desktop"}""", "$.viewport")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}],"planning":{"model":"m","tokens":3}}""", "$.planning.tokens")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"text-visible","text":"a","exact":true}}]}""", "$.steps[0].expectedState.exact")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"text-absent","text":"a","exact":true}}]}""", "$.steps[0].expectedState.exact")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"testid-visible","testId":"a","exact":true}}]}""", "$.steps[0].expectedState.exact")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/","exact":true}}]}""", "$.steps[0].expectedState.exact")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"input-value","testId":"a","value":"b","exact":true}}]}""", "$.steps[0].expectedState.exact")]
    [InlineData("""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"goto","target":"/","expectedState":{"exact":true,"path":"/","kind":"url-path"}}]}""", "$.steps[0].expectedState.exact")]
    public void An_unknown_key_anywhere_in_a_script_is_refused_and_named(string json, string location)
    {
        var error = Read(json).Error;

        Assert.Equal((EvidenceErrors.ScriptInvalid.Code, location + ": unknown property"), (error.Code, error.Message));
    }

    [Fact, Trait("Lane", "unit")]
    public void Missing_planning_usage_stays_unknown_rather_than_zero()
    {
        var script = Read("""{"route":"/","role":"staff","viewports":["phone"],"steps":[{"action":"goto","target":"/","expectedState":{"kind":"url-path","path":"/"}}],"planning":{"model":"m"}}""").Value;

        Assert.Equal("m", script.Planning?.Model);
        Assert.Null(script.Planning?.InputTokens);
        Assert.Null(script.Planning?.OutputTokens);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_describes_its_action_and_expectation_for_repair() =>
        Assert.Equal("fill search; expected: input search has value \"St\"", Read(Valid).Value.Steps[1].Describe());

    [Theory, Trait("Lane", "unit")]
    [InlineData(ViewportKind.Desktop, 1280, 800)]
    [InlineData(ViewportKind.Phone, 375, 812)]
    public void Named_viewports_have_fixed_sizes(ViewportKind kind, int w, int h) =>
        Assert.Equal(new(w, h), ScriptViewports.SizeOf(kind));
}
