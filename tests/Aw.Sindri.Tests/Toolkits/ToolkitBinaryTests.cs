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

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_dir, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
