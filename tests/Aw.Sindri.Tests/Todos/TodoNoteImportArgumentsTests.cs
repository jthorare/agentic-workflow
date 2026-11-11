using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Todos;

/// <summary>'sindri todo note import [--expect N] [--dry-run] [--json]' as the todo verbs read it (todo spec §4.4, §9 TP-13).</summary>
public sealed class TodoNoteImportArgumentsTests
{
    private static readonly DateOnly _today = new(2026, 11, 8);

    private static Result<TodoVerbArguments.Parsed> Parse(params string[] args) => TodoVerbArguments.Parse(["todo", "note", "import", .. args], _today);

    [Fact, Trait("Lane", "unit")]
    public void Expect_alone_is_an_import_of_that_count()
    {
        var parsed = Parse("--expect", "2").Value;

        Assert.Equal((TodoVerbArguments.Verb.NoteImport, 2, false, false), (parsed.Verb, parsed.Expect, parsed.DryRun, parsed.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dry_run_needs_no_count_and_takes_the_json_and_no_start_flags()
    {
        var parsed = Parse("--dry-run", "--json", "--no-start").Value;

        Assert.Equal((null, true, true, true), (parsed.Expect, parsed.DryRun, parsed.Json, parsed.NoStart));
    }

    [Fact, Trait("Lane", "unit")]
    public void Neither_a_count_nor_a_dry_run_is_refused_with_the_fix()
    {
        var refused = Parse();

        Assert.Equal(SindriErrors.InvalidInput.Code, refused.Error.Code);
        Assert.Contains("--dry-run", refused.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--expect")]
    [InlineData("--expect", "many")]
    [InlineData("--expect", "-1")]
    public void A_count_that_is_missing_or_not_a_whole_number_is_refused(params string[] args) =>
        Assert.Equal(SindriErrors.InvalidInput.Code, Parse(args).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_stray_word_is_refused() =>
        Assert.Equal(SindriErrors.InvalidInput.Code, Parse("--dry-run", "now").Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void The_import_flags_belong_to_the_import_only()
    {
        Assert.Equal(SindriErrors.InvalidInput.Code, TodoVerbArguments.Parse(["todo", "done", "td_1", "--dry-run"], _today).Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, TodoVerbArguments.Parse(["todo", "note", "--expect", "2"], _today).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_plain_note_verb_still_reads_as_the_state_read() =>
        Assert.Equal(TodoVerbArguments.Verb.Note, TodoVerbArguments.Parse(["todo", "note", "--json"], _today).Value.Verb);
}
