using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>What every <see cref="ISpecFolders"/> does: lists the folders in name order with every entry (gaps/ entries as 'gaps/x.cfg'), reports a link as a link without reading it, and lists nothing when there is nothing.</summary>
public abstract class SpecFoldersContract
{
    private const string Pattern = "specs/tla/*/";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Makes a workspace that holds exactly these folders.</summary>
    /// <param name="folders">The folders, each with a workspace-relative path.</param>
    /// <returns>The port and the workspace to ask it about.</returns>
    protected abstract (ISpecFolders Folders, string Workspace) Arrange(IReadOnlyList<SpecFolder> folders);

    [Fact]
    public async Task Folders_come_back_in_name_order_with_every_entry_and_its_text()
    {
        var (folders, workspace) = Arrange(
        [
            Folder("Takeover", new SpecFile("Takeover.tla", SpecFileKind.Regular, "---- MODULE Takeover ----\n"), new SpecFile("gaps/Bug.cfg", SpecFileKind.Regular, "\\* witness: P\n")),
            Folder("Retry", new SpecFile("README.md", SpecFileKind.Regular, "models: src/a.cs\n")),
        ]);

        var listed = await folders.List(workspace, Pattern, Token);

        Assert.Equal(["specs/tla/Retry", "specs/tla/Takeover"], listed.Value.Select(static f => f.Path), StringComparer.Ordinal);
        var takeover = listed.Value[1].Files.OrderBy(static f => f.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(["Takeover.tla", "gaps/Bug.cfg"], takeover.Select(static f => f.Name), StringComparer.Ordinal);
        Assert.Equal(["---- MODULE Takeover ----\n", "\\* witness: P\n"], takeover.Select(static f => f.Text), StringComparer.Ordinal);
        Assert.All(takeover, static f => Assert.Equal(SpecFileKind.Regular, f.Kind));
    }

    [Fact]
    public async Task A_link_is_reported_as_a_link_and_is_not_read()
    {
        var (folders, workspace) = Arrange([Folder("Linked", new SpecFile("Evil.jar", SpecFileKind.Symlink, string.Empty), new SpecFile("Linked.tla", SpecFileKind.Regular, "x"))]);

        var listed = await folders.List(workspace, Pattern, Token);

        var link = Assert.Single(listed.Value.Single().Files, static f => f.Name == "Evil.jar");
        Assert.Equal((SpecFileKind.Symlink, string.Empty), (link.Kind, link.Text));
    }

    [Fact]
    public async Task A_workspace_with_no_spec_folders_lists_none()
    {
        var (folders, workspace) = Arrange([]);

        var listed = await folders.List(workspace, Pattern, Token);

        Assert.Empty(listed.Value);
    }

    [Fact]
    public async Task A_file_and_a_folder_of_the_workspace_exist_and_a_missing_path_does_not()
    {
        var (folders, workspace) = Arrange([Folder("Takeover", new SpecFile("Takeover.tla", SpecFileKind.Regular, "x"))]);

        Assert.True(await folders.Exists(workspace, "specs/tla/Takeover/Takeover.tla", Token));
        Assert.True(await folders.Exists(workspace, "specs/tla/Takeover/", Token));
        Assert.False(await folders.Exists(workspace, "specs/tla/Takeover/Nope.tla", Token));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("specs/../../outside.txt")]
    [InlineData("/etc/hostname")]
    public async Task A_path_that_climbs_out_of_the_workspace_or_starts_at_the_root_does_not_exist(string path)
    {
        var (folders, workspace) = Arrange([Folder("Takeover", new SpecFile("Takeover.tla", SpecFileKind.Regular, "x"))]);

        Assert.False(await folders.Exists(workspace, path, Token));
    }

    [Fact]
    public async Task A_link_to_something_outside_the_workspace_does_not_exist()
    {
        var (folders, workspace) = Arrange([Folder("Linked", new SpecFile("Evil.jar", SpecFileKind.Symlink, string.Empty))]);

        Assert.False(await folders.Exists(workspace, "specs/tla/Linked/Evil.jar", Token));
    }

    private static SpecFolder Folder(string name, params SpecFile[] files) => new($"specs/tla/{name}", files);
}
