using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

/// <summary>'context.docs.directoryNames' (owned index spec U18b, map structure spec §11.2): file names that count as docs in any directory.</summary>
public sealed class DocDirectoryNamesSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void With_no_key_the_names_are_AGENTS_README_and_CONTEXT()
    {
        Assert.Equal(["AGENTS.md", "README.md", "CONTEXT.md"], SettingsReader.Read(section: null).Value.DocDirectoryNames, StringComparer.Ordinal);
        Assert.Equal(["AGENTS.md", "README.md", "CONTEXT.md"], Read("""{"docs":{"globs":["docs/**/*.md"]}}""").Value.DocDirectoryNames, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_written_list_replaces_the_defaults_and_an_empty_one_turns_the_names_off()
    {
        Assert.Equal(["NOTES.md"], Read("""{"docs":{"directoryNames":["NOTES.md"]}}""").Value.DocDirectoryNames, StringComparer.Ordinal);
        Assert.Empty(Read("""{"docs":{"directoryNames":[]}}""").Value.DocDirectoryNames);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"docs":{"directoryNames":["docs/NOTES.md"]}}""")]
    [InlineData("""{"docs":{"directoryNames":["*.md"]}}""")]
    [InlineData("""{"docs":{"directoryNames":[".."]}}""")]
    [InlineData("""{"docs":{"directoryNames":[""]}}""")]
    [InlineData("""{"docs":{"directoryNames":[null]}}""")]
    [InlineData("""{"docs":{"directoryNames":["A.md","A.md"]}}""")]
    public void A_name_that_is_not_a_plain_file_name_or_is_listed_twice_is_SND_CONTEXT_001_at_its_key(string json)
    {
        var settings = Read(json);

        Assert.Equal("SND-CONTEXT-001", settings.Error.Code);
        Assert.StartsWith("context.docs.directoryNames", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_indexed_globs_are_the_doc_globs_plus_a_glob_per_name_that_matches_any_directory()
    {
        var settings = Read("""{"docs":{"globs":["docs/**/*.md"],"directoryNames":["NOTES.md","README.md"]}}""").Value;

        Assert.Equal(["docs/**/*.md", "**/NOTES.md", "**/README.md"], settings.IndexedDocGlobs, StringComparer.Ordinal);
        Assert.Equal(["docs/**/*.md"], settings.DocGlobs, StringComparer.Ordinal);   // why: the record globs themselves are not widened
    }
}
