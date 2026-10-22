using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The pure rules of adding a repo (setup-once spec §5.1): the default name, the job key, and the folders that can never be a repo.</summary>
public sealed class RepoFoldersTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("/home/me/agentic-workflow", "agentic-workflow")]
    [InlineData("/home/me/My Project", "my-project")]
    [InlineData("/home/me/api_v2.0", "api-v2-0")]
    [InlineData("/home/me/a---b", "a-b")]
    [InlineData("/home/me/demo/", "demo")]
    [InlineData("/home/me/.hidden", "hidden")]
    [InlineData("/home/me/__", "repo")]
    [InlineData("/", "repo")]
    public void The_default_name_is_the_folder_in_lowercase_with_each_run_of_other_characters_one_dash(string path, string expected)
    {
        Assert.Equal(expected, RepoFolders.NameOf(path));
        Assert.True(RepoName.TryFrom(RepoFolders.NameOf(path), out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_name_is_at_most_100_characters_and_still_a_repo_name()
    {
        var name = RepoFolders.NameOf("/home/me/" + new string('a', 99) + "-" + new string('b', 50));

        Assert.True(name.Length <= 100);
        Assert.False(name.EndsWith('-'));
        Assert.True(RepoName.TryFrom(name, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_job_key_is_the_first_16_hex_characters_of_the_sha256_of_the_path_and_never_the_path()
    {
        var key = RepoFolders.KeyOf("/home/me/demo").Value;

        Assert.Matches("^[0-9a-f]{16}$", key);
        Assert.Equal(key, RepoFolders.KeyOf("/home/me/demo").Value);
        Assert.NotEqual(key, RepoFolders.KeyOf("/home/me/other").Value, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home/me", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home/me/", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home/me/.agentic-workflow/sindri", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home/me/.agentic-workflow/sindri/work", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home/me/.agentic-workflow", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home", "/home/me", "/home/me/.agentic-workflow/sindri", true)]
    [InlineData("/home/me/repos/demo", "/home/me", "/home/me/.agentic-workflow/sindri", false)]
    [InlineData("/home/me/.agentic-workflow-two", "/home/me", "/home/me/.agentic-workflow/sindri", false)]
    [InlineData("/home/me/.agentic-workflow/sindri-two", "/home/me", "/home/me/.agentic-workflow/sindri", false)]
    public void Some_folders_are_never_a_repo(string path, string userHome, string sindriHome, bool refused)
    {
        var reason = RepoFolders.Refuse(path, userHome, sindriHome);

        Assert.Equal(refused, reason is not null);
    }
}
