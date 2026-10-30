using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.SetProfileKey;

/// <summary>'profile set' (#1740) on strings: one scalar line changes (or the lines for new parents appear) and every other byte of the file stays.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileKeyTextTests
{
    private const string Pick = "context.models.labeling.pick";

    [Fact]
    public void An_existing_scalar_is_replaced_on_its_line_and_its_trailing_comment_stays()
    {
        const string Before = "# mine\ncontext:\n  models:\n    labeling:\n      pick: off   # was off\n      candidates: 3\n";

        var edited = ProfileKeyText.Edit(Before, Pick, "jev");

        Assert.Equal("# mine\ncontext:\n  models:\n    labeling:\n      pick: jev   # was off\n      candidates: 3\n", edited.Value);
    }

    [Fact]
    public void A_new_key_goes_at_the_end_of_its_existing_block_before_the_blank_line_that_follows()
    {
        const string Before = "context:\n  models:\n    labeling:\n      candidates: 3\n\n  sessions:\n    enabled: true\n";

        var edited = ProfileKeyText.Edit(Before, Pick, "jev");

        Assert.Equal("context:\n  models:\n    labeling:\n      candidates: 3\n      pick: jev\n\n  sessions:\n    enabled: true\n", edited.Value);
    }

    [Fact]
    public void Missing_parents_are_created_in_block_style_at_the_end_of_their_parent_block()
    {
        const string Before = "schemaVersion: 1\ncontext:\n  sessions:\n    enabled: true\nuser: me\n";

        var edited = ProfileKeyText.Edit(Before, Pick, "jev");

        Assert.Equal("schemaVersion: 1\ncontext:\n  sessions:\n    enabled: true\n  models:\n    labeling:\n      pick: jev\nuser: me\n", edited.Value);
    }

    [Fact]
    public void A_missing_top_level_parent_is_appended_to_the_file()
    {
        var edited = ProfileKeyText.Edit("schemaVersion: 1\nuser: me\n", Pick, "jev");

        Assert.Equal("schemaVersion: 1\nuser: me\ncontext:\n  models:\n    labeling:\n      pick: jev\n", edited.Value);
    }

    [Fact]
    public void A_file_with_comments_and_blank_lines_everywhere_changes_only_the_one_line()
    {
        const string Before = "# top\n\nschemaVersion: 1   # version\n\n# the context\ncontext:   # section\n\n  # models\n  models:\n    # labeling\n    labeling:\n\n      # the pick\n      pick: 'off'   # keep\n\n      candidates: 3\n  # tail\nuser: \"me\"\n";

        var edited = ProfileKeyText.Edit(Before, Pick, "jev");

        Assert.Equal(Before.Replace("pick: 'off'   # keep", "pick: jev   # keep", StringComparison.Ordinal), edited.Value);
    }

    [Fact]
    public void A_key_that_already_has_the_value_leaves_the_text_as_it_was()
    {
        const string Before = "context:\n  models:\n    labeling:\n      pick: jev   # yes\n";

        Assert.Equal(Before, ProfileKeyText.Edit(Before, Pick, "jev").Value);
        Assert.Equal(Before, ProfileKeyText.Edit(Before.Replace("jev", "\"jev\"", StringComparison.Ordinal), Pick, "jev").Value.Replace("\"jev\"", "jev", StringComparison.Ordinal));
    }

    [Fact]
    public void Crlf_line_endings_stay()
    {
        const string Before = "context:\r\n  models:\r\n    labeling:\r\n      pick: off\r\n";

        Assert.Equal("context:\r\n  models:\r\n    labeling:\r\n      pick: jev\r\n", ProfileKeyText.Edit(Before, Pick, "jev").Value);
        Assert.Equal("context:\r\n  models:\r\n    labeling:\r\n      pick: off\r\n      candidates: 3\r\n", ProfileKeyText.Edit(Before, "context.models.labeling.candidates", "3").Value);
    }

    [Fact]
    public void A_flow_style_parent_is_refused_with_its_key_path()
    {
        const string Before = "context:\n  models:\n    labeling: { pick: off }\n";

        var edited = ProfileKeyText.Edit(Before, Pick, "jev");

        Assert.Equal("SND-PROFILE-002", edited.Error.Code);
        Assert.Contains("context.models.labeling", edited.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("context.models.labeling", "context:\n  models:\n    labeling:\n      pick: off\n")]
    [InlineData("context.models.labeling", "context:\n  models:\n    labeling: { pick: off }\n")]
    [InlineData("context.repos", "context:\n  repos:\n    - web\n    - api\n")]
    [InlineData("context.repos", "context:\n  repos: [web, api]\n")]
    public void A_map_or_list_at_the_key_is_refused_with_its_key_path(string key, string before)
    {
        var edited = ProfileKeyText.Edit(before, key, "x");

        Assert.Equal("SND-PROFILE-002", edited.Error.Code);
        Assert.Contains(key, edited.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("context..pick")]
    [InlineData(".context")]
    [InlineData("context.")]
    [InlineData("context.a b")]
    [InlineData("context.a:b")]
    public void A_key_that_is_not_a_dotted_path_is_refused(string key) =>
        Assert.Equal("SND-CLI-003", ProfileKeyText.Edit("user: me\n", key, "x").Error.Code);

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("12", "12")]
    [InlineData("-3", "-3")]
    [InlineData("0.5", "0.5")]
    [InlineData("jev", "jev")]
    [InlineData("off", "off")]
    [InlineData("claude-code/sonnet", "claude-code/sonnet")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("null", "\"null\"")]
    [InlineData("yes", "\"yes\"")]
    [InlineData("", "\"\"")]
    [InlineData("x: y", "\"x: y\"")]
    [InlineData("#hash", "\"#hash\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    public void A_value_is_quoted_only_when_plain_would_read_as_something_else(string value, string scalar) =>
        Assert.Equal(scalar, ProfileKeyText.Scalar(value));
}
