using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>UI spec E-16: a catalog CLI form is 'sindri' and checked parts only, so a copied command can't carry a newline, a bidi override or a second command.</summary>
public sealed class CliFormTests
{
    public static TheoryData<string> Shipped => [.. new CommandCatalog().Rows.Select(row => row.Cli.Text)];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Shipped))]
    [InlineData("sindri")]
    [InlineData("sindri open <itemId> --attach [--agent <agent>]")]
    [InlineData("sindri request <kind> [--item <itemId>] <text>")]
    public void Parse_accepts_words_flags_placeholders_and_optional_parts(string text) =>
        Assert.Equal(text, CliForm.Parse(text).Value.Text);

    [Theory, Trait("Lane", "unit")]
    [InlineData("sindri stop\n", "U+000A")]
    [InlineData("sindri stop\u0007", "U+0007")]
    [InlineData("sindri\tstop", "U+0009")]
    [InlineData("sindri [--note <note>\u202E]", "U+202E")]
    [InlineData("sindri \u2066stop", "U+2066")]
    [InlineData("sindri\u200Bstop", "U+200B")]
    [InlineData("sindri stop\uFEFF", "U+FEFF")]
    [InlineData("sindri stop\u2028", "U+2028")]
    [InlineData("sindri\u00A0stop", "U+00A0")]
    public void Parse_refuses_control_bidi_and_invisible_characters(string text, string named)
    {
        var refused = CliForm.Parse(text);

        Assert.Equal("SND-CLI-003", refused.Error.Code);
        Assert.Contains(named, refused.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(text, refused.Error.Message, StringComparison.Ordinal);   // the raw text never reaches a log or a terminal
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("sindri stop ")]
    [InlineData("sindri  stop")]
    [InlineData("sindri Stop")]
    [InlineData("sindri stop; rm -rf ~")]
    [InlineData("sindri stop && sindri doctor")]
    [InlineData("sindri <a b>")]
    [InlineData("sindri []")]
    [InlineData("sindri [anything goes here, even (this)]")]
    [InlineData("sindri [--note \"x\"]")]
    [InlineData("sindri [--a [--b]]")]
    [InlineData("sindri st\u00F6p")]
    [InlineData("judge stop")]
    public void Parse_refuses_free_text_and_a_second_command(string text) =>
        Assert.Equal("SND-CLI-003", CliForm.Parse(text).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void An_id_at_hash_part_is_a_form_and_fills_both_placeholders()
    {
        var form = CliForm.Parse("sindri recipe approve <recipe>@<hash>").Value;
        var filled = form.Fill("recipe", Core.Workflow.Ids.RecipeId.From("workflow.ship")).Value.Fill("hash", ProfileHash.From("3f1c2a" + new string('0', 58))).Value;

        Assert.Equal("sindri recipe approve workflow.ship@3f1c2a" + new string('0', 58), filled.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void Fill_substitutes_a_typed_id_for_its_placeholder()
    {
        var hash = ProfileHash.From("3f1c2a" + new string('0', 58));

        var approve = CliForm.Parse("sindri profile approve <hash>").Value.Fill("hash", hash);
        var scoped = CliForm.Parse("sindri item add <title> [--repo <repo>]").Value.Fill("repo", RepoName.From("agentic-workflow"));

        Assert.Equal("sindri profile approve " + hash.Value, approve.Value.Text);
        Assert.Equal("sindri item add <title> [--repo agentic-workflow]", scoped.Value.Text);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("hash")]
    [InlineData("repo>")]
    [InlineData("")]
    public void Fill_refuses_a_placeholder_the_form_lacks(string placeholder) =>
        Assert.Equal("SND-CLI-003", CliForm.Parse("sindri item add <title> [--repo <repo>]").Value.Fill(placeholder, RepoName.From("aw")).Error.Code);

    [Theory, Trait("Lane", "unit")]
    [InlineData("x\nsindri stop")]
    [InlineData("a b")]
    [InlineData("-rf")]
    [InlineData("$(id)")]
    [InlineData("a\u202Eb")]
    [InlineData("")]
    public void Fill_refuses_an_id_whose_text_could_break_the_line(string value)
    {
        var refused = CliForm.Parse("sindri item close <itemId>").Value.Fill("itemId", new LooseId(value));

        Assert.Equal("SND-CLI-003", refused.Error.Code);
        Assert.DoesNotContain("sindri stop", refused.Error.Message, StringComparison.Ordinal);
    }
}
