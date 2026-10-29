using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.IndexSources;

// Owned-index spec §5.4: the pointer is written under context.sources.github and every other key and comment stays as it was.
[Lane(TestLane.Integration)]
public sealed class YamlSourceTokenProfileWriterTests : IDisposable
{
    private const string Pointer = "keychain:sindri-index/github";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-source-profile-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string ProfilePath => Path.Combine(_dir, "profile", "profile.yaml");

    private YamlSourceTokenProfileWriter Writer() => new(new SindriSettings(_dir, ProfilePath));

    private void Write(string yaml)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        File.WriteAllText(ProfilePath, yaml);
    }

    [Fact]
    public void The_pointer_is_added_as_the_first_line_of_the_github_block_and_everything_else_stays()
    {
        Write("# mine\nschemaVersion: 1\nuser: me\ncontext:\n  sources:\n    github:\n      repos: [acme/widgets]   # scope\n    linear: { teams: [ENG] }\n");

        Assert.True(Writer().SetToken("github", Pointer).Value);

        Assert.Equal("# mine\nschemaVersion: 1\nuser: me\ncontext:\n  sources:\n    github:\n      token: \"keychain:sindri-index/github\"\n      repos: [acme/widgets]   # scope\n    linear: { teams: [ENG] }\n", File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void An_existing_pointer_line_is_replaced_and_a_rerun_changes_nothing()
    {
        Write("schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    github:\n      repos: [acme/widgets]\n      token: \"env:OLD\"\n");

        Assert.True(Writer().SetToken("github", Pointer).Value);
        Assert.False(Writer().SetToken("github", Pointer).Value);

        Assert.Contains("      token: \"keychain:sindri-index/github\"\n", File.ReadAllText(ProfilePath), StringComparison.Ordinal);
        Assert.DoesNotContain("env:OLD", File.ReadAllText(ProfilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_without_the_github_entry_is_refused_and_left_alone()
    {
        const string Yaml = "schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    linear: { teams: [ENG] }\n";
        Write(Yaml);

        var result = Writer().SetToken("github", Pointer);

        Assert.Equal(SindriErrors.IndexNotInProfile.Code, result.Error.Code);
        Assert.Equal(Yaml, File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void A_flow_style_github_entry_is_refused_and_left_alone()
    {
        const string Yaml = "schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    github: { repos: [acme/widgets] }\n";
        Write(Yaml);

        var result = Writer().SetToken("github", Pointer);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, result.Error.Code);
        Assert.Equal(Yaml, File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void An_empty_teams_line_is_replaced_by_the_list_and_everything_else_stays()
    {
        Write("# mine\nschemaVersion: 1\ncontext:\n  sources:\n    linear:\n      token: \"env:X\"\n      teams: []   # none yet\n    github:\n      repos: [a/b]\n");

        Assert.True(Writer().SetTeams("linear", ["ENG", "OPS"]).Value);
        Assert.False(Writer().SetTeams("linear", ["ENG", "OPS"]).Value);

        Assert.Equal("# mine\nschemaVersion: 1\ncontext:\n  sources:\n    linear:\n      token: \"env:X\"\n      teams: [ENG, OPS]\n    github:\n      repos: [a/b]\n", File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void A_block_without_a_teams_line_gets_one_as_its_first_line()
    {
        Write("schemaVersion: 1\ncontext:\n  sources:\n    linear:\n      token: \"env:X\"\n");

        Assert.True(Writer().SetTeams("linear", ["ENG"]).Value);

        Assert.Equal("schemaVersion: 1\ncontext:\n  sources:\n    linear:\n      teams: [ENG]\n      token: \"env:X\"\n", File.ReadAllText(ProfilePath));
    }

    [Fact]
    public void Teams_without_a_linear_entry_are_refused_and_the_file_is_left_alone()
    {
        const string Yaml = "schemaVersion: 1\ncontext:\n  sources:\n    github:\n      repos: [a/b]\n";
        Write(Yaml);

        Assert.Equal(SindriErrors.IndexNotInProfile.Code, Writer().SetTeams("linear", ["ENG"]).Error.Code);
        Assert.Equal(Yaml, File.ReadAllText(ProfilePath));
    }
}
