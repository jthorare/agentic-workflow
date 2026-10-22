using System.Text.Json;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri docs check --facts &lt;dir&gt; [--json]' as Program runs it (docs-sync spec API17): local, no control plane; exit 0 pass, 1 findings, 2 usage or tool error. It swaps the process's console.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class DocsCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public DocsCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string Facts => Path.Combine(_home.Path, "facts");

    private Task<int> Run(params string[] args) => CommandTable.Run(args, name => name == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System);

    [Fact, Trait("Lane", "integration")]
    public async Task Clean_docs_exit_0_and_json_prints_no_findings()
    {
        using var repo = await TempGitRepo.Create();
        await Stage(repo, "# A\n");

        Assert.Equal(0, await Run("docs", "check", "--facts", Facts, "--json"));

        using var body = JsonDocument.Parse(_out.ToString());
        Assert.True(body.RootElement.GetProperty("passed").GetBoolean());
        Assert.Equal(0, body.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_dead_link_exits_1_and_text_names_the_rule_with_its_fix()
    {
        using var repo = await TempGitRepo.Create();
        await Stage(repo, "# A\n[x](gone.md)\n");

        Assert.Equal(1, await Run("docs", "check", "--facts", Facts));

        Assert.Contains("DOCS-LINK-001", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("fix:", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_dead_link_with_json_exits_1_and_prints_the_finding()
    {
        using var repo = await TempGitRepo.Create();
        await Stage(repo, "# A\n[x](gone.md)\n");

        Assert.Equal(1, await Run("docs", "check", "--facts", Facts, "--json"));

        using var body = JsonDocument.Parse(_out.ToString());
        Assert.Equal("DOCS-LINK-001", body.RootElement.GetProperty("findings")[0].GetProperty("rule").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Missing_facts_a_missing_directory_and_a_bad_flag_exit_2()
    {
        _ = Directory.CreateDirectory(Facts);

        Assert.Equal(2, await Run("docs", "check", "--facts", Facts));
        Assert.Equal(2, await Run("docs", "check", "--facts", Path.Combine(_home.Path, "nowhere")));
        Assert.Equal(2, await Run("docs", "check"));
        Assert.Equal(2, await Run("docs", "check", "--facts", Facts, "--bogus"));
    }

    // The repo's worktree with one new doc, a base commit, and the fact files beside it.
    private async Task Stage(TempGitRepo repo, string markdown)
    {
        var baseCommit = await repo.Git("rev-parse", "HEAD");
        _ = Directory.CreateDirectory(Path.Combine(repo.Path, "docs"));
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "docs", "a.md"), markdown, TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Facts);
        await File.WriteAllTextAsync(Path.Combine(Facts, "workspace.json"), JsonSerializer.Serialize(repo.Path), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(Facts, "docsScope.json"), $$"""{"base":"{{baseCommit}}","paths":["docs/**"],"candidates":[]}""", TestContext.Current.CancellationToken);
    }
}
