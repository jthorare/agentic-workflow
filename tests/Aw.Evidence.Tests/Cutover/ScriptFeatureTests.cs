using Aw.Evidence.Core;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover gap G-a (spec §3.3): the script features the TypeScript runner had and the C# schema lacks. Red until unit 1.1 ports them;
/// each test is JSON in, so it needs no type the unit has yet to add. The tests without a skip already pass (today an unknown action, field
/// or state is refused, and a target is a free string): they are guards the unit must keep green, not red tests. Wording of the rules is SKILL.md 'Step actions and login',
/// 'Step targets' and 'Step checks'. Unit 1.1 also moves the 'known kind' list in ScriptReaderTests's message case.
/// </summary>
public sealed class ScriptFeatureTests
{
    private const string Visible = """{"kind":"testid-visible","testId":"panel"}""";

    private static readonly ResolvedHost _preview = new("https://pr-9.preview.example.com", HostKind.Preview);

    private static Kernel.Result<UiScript> Read(string steps, string extra = "") =>
        ScriptReader.Read(TestJson.Utf8($$"""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{{steps}}]{{extra}}}"""), TestJson.Options);

    private static string Step(string action, string target, string more = "", string expected = Visible) =>
        $$"""{"action":"{{action}}","target":"{{target}}"{{more}},"expectedState":{{expected}}}""";

    [Theory, Trait("Lane", "unit")]
    [InlineData("end")]
    [InlineData("start")]
    [InlineData("bottom")]
    [InlineData("top")]
    [InlineData("0")]
    [InlineData("240")]
    public void Scroll_moves_a_scroll_container_by_a_keyword_or_a_pixel_offset(string value)
    {
        var script = Read(Step("scroll", "timeline", ",\"value\":\"" + value + "\""));

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);
        Assert.Equal("scroll", script.Value.Steps[0].ActionName);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sideways")]
    [InlineData("-5")]
    [InlineData("1.5")]
    public void Scroll_with_any_other_value_fails_the_schema(string value) =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(Step("scroll", "timeline", ",\"value\":\"" + value + "\"")).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Drag_carries_its_destination_offsets_and_alt_through_a_round_trip()
    {
        var script = Read(Step("drag", "card-1", ",\"to\":\"lane-2\",\"grabOffset\":{\"x\":4,\"y\":8},\"dropOffset\":{\"x\":0,\"y\":12},\"holdAlt\":true"));

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);
        Assert.Equal("drag", script.Value.Steps[0].ActionName);
        var written = TestJson.Write(script.Value);
        Assert.Contains("\"to\":\"lane-2\"", written, StringComparison.Ordinal);
        Assert.Contains("\"grabOffset\":{\"x\":4,\"y\":8}", written, StringComparison.Ordinal);
        Assert.Contains("\"dropOffset\":{\"x\":0,\"y\":12}", written, StringComparison.Ordinal);
        Assert.Contains("\"holdAlt\":true", written, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("drag", "card-1", "")]                                          // no destination
    [InlineData("click", "card-1", ",\"to\":\"lane-2\"")]                        // 'to' belongs to drag
    [InlineData("click", "card-1", ",\"holdAlt\":true")]                         // 'holdAlt' belongs to drag
    [InlineData("fill", "card-1", ",\"grabOffset\":{\"x\":1,\"y\":1}")]          // offsets belong to drag
    [InlineData("press", "card-1", "")]                                         // no key
    [InlineData("click", "card-1", ",\"key\":\"Enter\"")]                        // 'key' belongs to press
    [InlineData("drag", "card-1", ",\"to\":\"lane-2\",\"grabOffset\":{\"x\":1}")] // an offset needs both coordinates
    public void A_step_field_on_the_wrong_action_or_a_missing_one_fails_the_schema(string action, string target, string more) =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(Step(action, target, more)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Press_sends_a_key_to_the_target_or_to_whatever_has_focus()
    {
        var withTarget = Read(Step("press", "tab-1", ",\"key\":\"ArrowRight\""));
        var focused = ScriptReader.Read(
            TestJson.Utf8($$"""{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"action":"press","key":"Shift+Tab","expectedState":{{Visible}}}]}"""),
            TestJson.Options);

        Assert.True(withTarget.IsSuccess, withTarget.IsError ? withTarget.Error.Message : string.Empty);
        Assert.True(focused.IsSuccess, focused.IsError ? focused.Error.Message : string.Empty);
        Assert.Equal("press", focused.Value.Steps[0].ActionName);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("click", true)]
    [InlineData("fill", true)]
    [InlineData("scroll", true)]
    [InlineData("goto", false)]
    [InlineData("expect-visible", false)]
    public void Index_picks_the_nth_match_of_a_repeated_test_id_on_click_fill_and_scroll_only(string action, bool accepted)
    {
        var result = Read(Step(action, action == "goto" ? "/x" : "row-open", ",\"index\":1"));

        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
        {
            Assert.Equal(EvidenceErrors.ScriptInvalid.Code, result.Error.Code);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_negative_index_fails_the_schema() =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(Step("click", "row-open", ",\"index\":-1")).Error.Code);

    [Theory, Trait("Lane", "unit")]
    [InlineData("role:button|Save")]
    [InlineData("role:option|Unit 4200")]
    [InlineData("role:option|07:00-19:30 Day#1")]    // a trailing #<n> (1-based) picks the nth match
    [InlineData("role:tab|A|B")]                      // the role is split at the first '|'; the name may hold '|'
    [InlineData("text:Unit 4200")]
    [InlineData("text:Day#2")]
    [InlineData("role:button|Shift #12")]            // a space before '#': the name is taken literally
    public void Prefixed_click_targets_are_accepted(string target) =>
        Assert.True(Read(Step("click", target)).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("role:button")]         // no name
    [InlineData("text:")]               // no text
    [InlineData("Role:button|Save")]    // near-miss prefix
    [InlineData(" role:button|Save")]   // a leading space
    [InlineData("role:Button|Save")]    // the role is lower-case
    [InlineData("role:button|Day#0")]   // #<n> is 1-based
    public void A_malformed_or_near_miss_prefix_fails_the_schema_parse(string target) =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(Step("click", target)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Expected_state_url_search_waits_for_one_query_parameter()
    {
        var script = Read(Step("click", "filter-night", expected: """{"kind":"url-search","param":"timeFilter","value":"Night"}"""));

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);
        Assert.Contains("\"kind\":\"url-search\"", TestJson.Write(script.Value), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Expected_state_testid_absent_passes_when_no_visible_element_has_the_test_id()
    {
        var script = Read(Step("drag", "card-1", ",\"to\":\"lane-2\"", """{"kind":"testid-absent","testId":"drag-ghost"}"""));

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);
        Assert.Contains("\"kind\":\"testid-absent\"", TestJson.Write(script.Value), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"kind":"url-search","value":"Night"}""")]          // no param
    [InlineData("""{"kind":"url-search","param":"","value":"Night"}""")]
    [InlineData("""{"kind":"testid-absent"}""")]                        // no test id
    public void A_new_expected_state_missing_its_fields_fails_the_schema(string expected) =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(Step("click", "filter-night", expected: expected)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_script_level_login_signs_in_by_environment_variable_names_and_selectors()
    {
        var script = Read(
            Step("goto", "/x", expected: """{"kind":"url-path","path":"/x"}"""),
            ""","login":{"emailEnv":"ADMIN_EMAIL","passwordEnv":"ADMIN_PASSWORD","nextSelector":"button.next","loginPath":"/signin"}""");

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);
        var written = TestJson.Write(script.Value);
        Assert.Contains("\"emailEnv\":\"ADMIN_EMAIL\"", written, StringComparison.Ordinal);
        Assert.Contains("\"loginPath\":\"/signin\"", written, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"emailEnv":"admin_email","passwordEnv":"ADMIN_PASSWORD"}""")]                        // env names are UPPER_SNAKE
    [InlineData("""{"emailEnv":"ADMIN_EMAIL"}""")]                                                       // both are required
    [InlineData("""{"emailEnv":"A_EMAIL","passwordEnv":"A_PASSWORD","password":"hunter2"}""")]            // unknown keys fail: no literal password
    public void A_malformed_login_fails_the_schema(string login) =>
        Assert.Equal(
            EvidenceErrors.ScriptInvalid.Code,
            Read(Step("goto", "/x", expected: """{"kind":"url-path","path":"/x"}"""), ",\"login\":" + login).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Script_level_local_storage_is_a_map_of_strings_written_verbatim()
    {
        var script = Read(Step("click", "open"), ",\"localStorage\":{\"theme\":\"dark\",\"flags\":\"{\\\"newNav\\\":true}\"}");

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);
        Assert.Contains("\"theme\":\"dark\"", TestJson.Write(script.Value), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_local_storage_value_that_is_not_a_string_fails_the_schema() =>
        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, Read(Step("click", "open"), ",\"localStorage\":{\"flag\":true}").Error.Code);

    // The write guard (SKILL 'Host override'): a preview host refuses the new actions that can write unless --allow-writes.
    [Theory, Trait("Lane", "unit")]
    [InlineData("drag", "card-1", ",\"to\":\"lane-2\"", true)]                 // every drag counts as a write
    [InlineData("press", "tab-1", ",\"key\":\"Enter\"", true)]                 // a non-navigation key can activate a Save button
    [InlineData("press", "tab-1", ",\"key\":\"Space\"", true)]
    [InlineData("press", "tab-1", ",\"key\":\"s\"", true)]
    [InlineData("press", "tab-1", ",\"key\":\"Control+S\"", true)]
    [InlineData("press", "tab-1", ",\"key\":\"ArrowRight\"", false)]           // navigation keys are read-only
    [InlineData("press", "tab-1", ",\"key\":\"Shift+Tab\"", false)]
    [InlineData("press", "tab-1", ",\"key\":\"Home\"", false)]
    [InlineData("press", "tab-1", ",\"key\":\"PageDown\"", false)]
    [InlineData("press", "tab-1", ",\"key\":\"Escape\"", false)]
    [InlineData("scroll", "timeline", ",\"value\":\"end\"", false)]            // scroll is read-only
    [InlineData("click", "role:button|Save", "", true)]                         // the name of a role: target is read by the keyword rule
    [InlineData("click", "text:Delete account", "", true)]
    [InlineData("click", "role:switch|Notifications", "", false)]               // no write word: not caught (documented limit)
    [InlineData("click", "role:option|Update Day#2", "", true)]                 // the #<n> suffix is dropped before the words are read
    [InlineData("click", "role:option|Unit 4200#1", "", false)]
    public void The_write_guard_reads_the_new_actions_and_prefixed_targets(string action, string target, string more, bool refused)
    {
        var script = Read(Step(action, target, more));
        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);

        var guarded = StepGuard.ForRun(script.Value.Steps, _preview, allowWrites: false);

        Assert.Equal(refused, guarded.IsError);
        Assert.True(StepGuard.ForRun(script.Value.Steps, _preview, allowWrites: true).IsSuccess);
        Assert.True(StepGuard.ForRun(script.Value.Steps, new ResolvedHost("http://localhost:3000", HostKind.Local), allowWrites: false).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parity_refuses_a_drag_and_a_key_press_without_allow_writes_on_every_host()
    {
        var script = Read(Step("drag", "card-1", ",\"to\":\"lane-2\""));
        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : string.Empty);

        Assert.Equal(EvidenceErrors.StepsRefused.Code, StepGuard.ForParity(script.Value.Steps, allowWrites: false).Error.Code);
    }
}
