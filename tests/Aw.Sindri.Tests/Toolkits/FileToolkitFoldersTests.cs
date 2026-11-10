using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The toolkits root on disk and the generator run (agent-first PR 16c): which folders count as toolkits, the argv the generator gets, and what a failure says.</summary>
[Lane(TestLane.Unit)]
public sealed class FileToolkitFoldersTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly TempHome _toolkit = new();

    public FileToolkitFoldersTests()
    {
        var scaffold = Directory.CreateDirectory(Path.Combine(_toolkit.Path, "scripts", "scaffold"));
        File.WriteAllText(Path.Combine(scaffold.FullName, "new-toolkit.mjs"), "// generator");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _home.Dispose();
        _toolkit.Dispose();
    }

    private string? Environment(string name) => name switch
    {
        "AW_TOOLKIT" => _toolkit.Path,
        _ => null,
    };

    private FileToolkitFolders Folders(FakeToolRunner runner) => new(_home.Path, Environment, runner);

    private static FakeToolRunner Exits(int code, string error = "") => new(new ToolRun(ToolOutcome.Exited, code, string.Empty, error));

    [Fact]
    public void Only_folders_that_hold_a_toolkit_project_are_listed_in_name_order()
    {
        var root = Path.Combine(_home.Path, "toolkits");
        foreach (var name in new[] { "zeta", "alpha", "empty" })
        {
            _ = Directory.CreateDirectory(Path.Combine(root, name));
        }

        File.WriteAllText(Path.Combine(root, "zeta", "Zeta.Toolkit.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "alpha", "Alpha.Toolkit.csproj"), "<Project />");

        var listed = Folders(Exits(0)).Folders();

        Assert.Equal(["alpha", "zeta"], listed.Select(static f => f.Name), StringComparer.Ordinal);
        Assert.Equal(Path.Combine(root, "alpha"), listed[0].Dir);
    }

    [Fact]
    public void A_box_with_no_toolkits_folder_lists_nothing() => Assert.Empty(Folders(Exits(0)).Folders());

    // A generator run that exits 0 after writing the project, the way new-toolkit.mjs leaves the folder.
    private static FakeToolRunner Writes() => new((_, arguments) =>
    {
        var dir = arguments[arguments.ToList().IndexOf("--dir") + 1];
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Made.Toolkit.csproj"), "<Project />");
        return new ToolRun(ToolOutcome.Exited, 0, string.Empty, string.Empty);
    });

    [Fact]
    public async Task The_generator_gets_a_pascal_case_name_the_tool_the_folder_the_feed_and_the_needs()
    {
        var runner = Writes();

        var made = await Folders(runner).Generate("github-triage", "list_issues", "connection:github", Token);

        Assert.True(made.IsSuccess);
        Assert.Equal(Path.Combine(_home.Path, "toolkits", "github-triage"), made.Value.Dir);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("node", call.FileName);
        Assert.Equal(
            [Path.Combine(_toolkit.Path, "scripts", "scaffold", "new-toolkit.mjs"), "--name", "GithubTriage", "--tool", "list_issues", "--dir", Path.Combine(_home.Path, "toolkits", "github-triage"),
                "--feed", Path.Combine(_home.Path, "sdk", "packages"), "--needs", "connection:github"],
            call.Arguments, StringComparer.Ordinal);
        Assert.Equal(_toolkit.Path, Assert.Single(runner.WorkingDirectories));
    }

    [Fact]
    public async Task A_failing_generator_is_SND_TK_013_with_what_it_said_on_standard_error()
    {
        var made = await Folders(Exits(1, "NT-SDK: no Aw.Sindri.Toolkit package in the SDK feed")).Generate("notes", "add_note", needs: null, Token);

        Assert.Equal(SindriErrors.ToolkitCreateFailed.Code, made.Error.Code);
        Assert.Contains("NT-SDK", made.Error.Message, StringComparison.Ordinal);
    }

    // #2692: through the ~/.agentic-workflow/toolkit link the generator's main check was false, so node exited 0 having written nothing.
    [Fact]
    public async Task A_generator_that_exits_0_and_leaves_no_toolkit_project_is_SND_TK_013_naming_the_folder()
    {
        var made = await Folders(Exits(0)).Generate("notes", "add_note", needs: null, Token);

        Assert.Equal(SindriErrors.ToolkitCreateFailed.Code, made.Error.Code);
        Assert.Contains(Path.Combine(_home.Path, "toolkits", "notes"), made.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_toolkit_checkout_without_the_generator_names_the_missing_file()
    {
        File.Delete(Path.Combine(_toolkit.Path, "scripts", "scaffold", "new-toolkit.mjs"));

        var made = await Folders(Exits(0)).Generate("notes", "add_note", needs: null, Token);

        Assert.Equal(SindriErrors.ToolkitCreateFailed.Code, made.Error.Code);
        Assert.Contains("new-toolkit.mjs", made.Error.Message, StringComparison.Ordinal);
    }
}
