using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>Which file is a toolkit's built binary (agent-first PR 16): the scaffold names the assembly '&lt;Name&gt;.Toolkit', so the name comes from the folder's one *.Toolkit.csproj, and a build and a start look the same way.</summary>
[Lane(TestLane.Integration)]
public sealed class ToolkitBinaryTests : IDisposable
{
    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "toolkit-fixtures", RealTime.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void The_binary_is_the_apphost_named_by_the_csproj_assembly_name_for_a_build_and_for_a_start()
    {
        Write("GithubTriage.Toolkit.csproj", "<Project><PropertyGroup><AssemblyName>GithubTriage.Toolkit</AssemblyName></PropertyGroup></Project>");
        Write("bin/Debug/net10.0/GithubTriage.Toolkit", string.Empty);
        Write("bin/Debug/net10.0/github-triage", string.Empty);   // a file that only shares the toolkit's name is not the binary
        var expected = Path.Combine(_dir, "bin/Debug/net10.0/GithubTriage.Toolkit");

        var forBuild = new FileToolkitWorkspace().BinaryOf(_dir);
        var forStart = ToolkitProcess.BinaryOf(new Toolkit("github-triage", _dir, ToolkitState.Built, "h", DateTimeOffset.UnixEpoch, Manifest: null, "human"));

        Assert.Equal(expected, forBuild.Value);
        Assert.Equal(expected, forStart);
    }

    [Fact]
    public void Without_an_assembly_name_the_csproj_file_name_is_used()
    {
        Write("Notes.Toolkit.csproj", "<Project />");
        Write("bin/Debug/net10.0/Notes.Toolkit", string.Empty);

        var found = new FileToolkitWorkspace().BinaryOf(_dir);

        Assert.EndsWith("Notes.Toolkit", found.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void No_csproj_or_two_of_them_is_SND_TK_003()
    {
        Write("bin/Debug/net10.0/Notes.Toolkit", string.Empty);
        var none = new FileToolkitWorkspace().BinaryOf(_dir);
        Write("A.Toolkit.csproj", "<Project />");
        Write("B.Toolkit.csproj", "<Project />");
        var two = new FileToolkitWorkspace().BinaryOf(_dir);

        Assert.Equal(SindriErrors.ToolkitManifestInvalid.Code, none.Error.Code);
        Assert.Equal(SindriErrors.ToolkitManifestInvalid.Code, two.Error.Code);
    }

    [Fact]
    public void A_scaffolded_toolkit_s_binary_is_under_artifacts_bin_in_its_own_project_folder_not_the_tests_copy()
    {
        // #2711: the scaffold sets UseArtifactsOutput; the tests project's output holds a newer copy of the apphost, which is not the one to run.
        Write("Notes.Toolkit.csproj", "<Project />");
        Write("artifacts/bin/Notes.Toolkit/debug/Notes.Toolkit", string.Empty);
        Write("artifacts/bin/Notes.Toolkit.Tests/debug/Notes.Toolkit", string.Empty);
        File.SetLastWriteTimeUtc(Path.Combine(_dir, "artifacts/bin/Notes.Toolkit/debug/Notes.Toolkit"), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var expected = Path.Combine(_dir, "artifacts/bin/Notes.Toolkit/debug/Notes.Toolkit");

        var forBuild = new FileToolkitWorkspace().BinaryOf(_dir);
        var forStart = ToolkitProcess.BinaryOf(new Toolkit("notes", _dir, ToolkitState.Built, "h", DateTimeOffset.UnixEpoch, Manifest: null, "human"));

        Assert.Equal(expected, forBuild.Value);
        Assert.Equal(expected, forStart);
    }

    [Fact]
    public void The_tests_project_is_the_one_toolkit_tests_csproj_one_folder_down()
    {
        Write("Notes.Toolkit.csproj", "<Project />");
        Write("Notes.Toolkit.Tests/Notes.Toolkit.Tests.csproj", "<Project />");
        Write("artifacts/obj/Notes.Toolkit.Tests/Other.Toolkit.Tests.csproj", "<Project />");   // deeper than one folder: not looked at

        var found = new FileToolkitWorkspace().TestsProjectOf(_dir);

        Assert.Equal("Notes.Toolkit.Tests/Notes.Toolkit.Tests.csproj", found.Value);
    }

    [Fact]
    public void No_tests_project_or_two_of_them_is_SND_TK_002()
    {
        Write("Notes.Toolkit.csproj", "<Project />");
        var none = new FileToolkitWorkspace().TestsProjectOf(_dir);
        Write("A.Toolkit.Tests/A.Toolkit.Tests.csproj", "<Project />");
        Write("B.Toolkit.Tests/B.Toolkit.Tests.csproj", "<Project />");
        var two = new FileToolkitWorkspace().TestsProjectOf(_dir);

        Assert.Equal(SindriErrors.ToolkitTestsFailed.Code, none.Error.Code);
        Assert.Equal(SindriErrors.ToolkitTestsFailed.Code, two.Error.Code);
    }

    [Fact]
    public async Task Build_output_and_test_results_do_not_change_the_source_hash_and_a_source_edit_does()
    {
        // #2711: every build rewrites artifacts/ and a test run writes TestResults/; neither is source, so a toolkit is unchanged right after its own build.
        Write("Notes.Toolkit.csproj", "<Project />");
        Write("NotesTools.cs", "class A;");
        Write("Notes.Toolkit.Tests/AddNoteTests.cs", "class T;");
        var workspace = new FileToolkitWorkspace();
        var before = await workspace.SourceHash(_dir, TestContext.Current.CancellationToken);

        Write("artifacts/bin/Notes.Toolkit/debug/Notes.Toolkit", "binary");
        Write("artifacts/obj/Notes.Toolkit/project.assets.json", "{}");
        Write("TestResults/run.log", "log");
        Write("Notes.Toolkit.Tests/bin/Debug/x.dll", "dll");
        Write("Notes.Toolkit.Tests/obj/project.assets.json", "{}");
        var afterBuild = await workspace.SourceHash(_dir, TestContext.Current.CancellationToken);
        Write("NotesTools.cs", "class B;");
        var afterEdit = await workspace.SourceHash(_dir, TestContext.Current.CancellationToken);

        Assert.Equal(before.Value, afterBuild.Value);
        Assert.NotEqual(before.Value, afterEdit.Value, StringComparer.Ordinal);
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_dir, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
