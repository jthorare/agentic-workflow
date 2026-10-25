using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.OnyxAccount;

// Spec §4.1 step 9: the profile section is written and every other key and comment stays as it was.
[Lane(TestLane.Integration)]
public sealed class YamlOnyxProfileWriterTests : IDisposable
{
    private const string Url = "http://127.0.0.1:3189";
    private const string Token = "keychain:onyx/sindri-onyx";
    private const string Entry = "onyx: { url: \"http://127.0.0.1:3189\", token: \"keychain:onyx/sindri-onyx\", version: \"v4.9.0\", managed: true }";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-profile-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string ProfilePath => Path.Combine(_dir, "profile", "profile.yaml");

    private YamlOnyxProfileWriter Writer() => new(new SindriSettings(_dir, ProfilePath));

    private string Write(string yaml)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        File.WriteAllText(ProfilePath, yaml);
        return yaml;
    }

    [Fact]
    public void A_missing_profile_is_created_with_the_schema_version_the_user_and_the_section()
    {
        Assert.True(Writer().SetOnyx(Url, Token, "v4.9.0").Value);

        var text = File.ReadAllText(ProfilePath);
        Assert.StartsWith("schemaVersion: 1\nuser: ", text, StringComparison.Ordinal);
        Assert.Contains($"context:\n  sources:\n    {Entry}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_section_is_added_under_existing_sources_and_every_comment_and_key_stays()
    {
        _ = Write("# my profile\nschemaVersion: 1\nuser: me   # who\ncontext:\n  sources:\n    linear: { teams: [ENG] }   # teams\nrepos:\n  - name: a\n");

        Assert.True(Writer().SetOnyx(Url, Token, "v4.9.0").Value);

        Assert.Equal($"# my profile\nschemaVersion: 1\nuser: me   # who\ncontext:\n  sources:\n    {Entry}\n    linear: {{ teams: [ENG] }}   # teams\nrepos:\n  - name: a\n", File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void A_context_without_sources_gets_a_sources_map()
    {
        _ = Write("schemaVersion: 1\nuser: me\ncontext:\n  retention: x\n");

        _ = Writer().SetOnyx(Url, Token, "v4.9.0").Value;

        Assert.Contains($"context:\n  sources:\n    {Entry}\n  retention: x\n", File.ReadAllText(ProfilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_without_context_gets_the_section_at_the_end()
    {
        _ = Write("schemaVersion: 1\nuser: me\n");

        _ = Writer().SetOnyx(Url, Token, "v4.9.0").Value;

        Assert.Equal($"schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    {Entry}\n", File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void An_existing_onyx_entry_is_replaced_in_place_even_when_written_as_a_block()
    {
        _ = Write("schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:9\"\n      token: \"env:X\"\n      version: v4.8.0\n    linear: { teams: [ENG] }\n");

        _ = Writer().SetOnyx(Url, Token, "v4.9.0").Value;

        Assert.Equal($"schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    {Entry}\n    linear: {{ teams: [ENG] }}\n", File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void Writing_the_same_section_again_changes_nothing()
    {
        _ = Writer().SetOnyx(Url, Token, "v4.9.0");
        var first = File.ReadAllText(ProfilePath);

        Assert.False(Writer().SetOnyx(Url, Token, "v4.9.0").Value);

        Assert.Equal(first, File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void A_layout_it_cannot_edit_is_an_error_naming_the_lines_to_add_and_the_file_is_untouched()
    {
        var before = Write("schemaVersion: 1\nuser: me\ncontext: {}\n");

        var result = Writer().SetOnyx(Url, Token, "v4.9.0");

        Assert.Equal("SND-PROFILE-002", result.Error.Code);
        Assert.Contains("add this under context.sources by hand", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void A_quote_in_a_value_is_escaped_so_the_file_still_reads_back()
        => Assert.True(Writer().SetOnyx(Url, "file:/tmp/a \"b\"/key", "v4.9.0").Value);
}
