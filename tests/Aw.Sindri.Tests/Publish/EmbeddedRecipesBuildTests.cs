using System.Diagnostics;
using System.Text.Json;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Publish;

/// <summary>
/// What the Host project embeds as recipes (setup-once spec section 5.3): the files of the staged directory <c xml:lang="en">AwRecipesDir</c>, or the checkout's
/// recipes/sindri when none is named (#878: a published binary with an empty set has nothing for approve-all to plan), and nothing else.
/// MSBuild evaluates the project (no compile), so the names are the ones the assembly would carry.
/// </summary>
public sealed class EmbeddedRecipesBuildTests
{
    private const string Prefix = "aw-recipes/sindri/";

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public void An_ordinary_build_embeds_the_checkouts_recipes_without_the_readme()
    {
        var names = RecipeResources(stagedDir: null, TestContext.Current.CancellationToken);

        Assert.Contains(Prefix + "workflow.scoping.yaml", names, StringComparer.Ordinal);
        Assert.DoesNotContain(Prefix + "README.md", names, StringComparer.Ordinal);
        Assert.Equal(
            Directory.EnumerateFiles(Path.Combine(RepoRoot(), "recipes", "sindri"), "*", SearchOption.AllDirectories)
                .Select(f => Prefix + Path.GetRelativePath(Path.Combine(RepoRoot(), "recipes", "sindri"), f).Replace('\\', '/'))
                .Where(static n => !string.Equals(n, Prefix + "README.md", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
            names,
            StringComparer.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public void An_empty_staged_directory_embeds_nothing()
    {
        using var home = new TempHome();
        var staged = Path.Combine(home.Path, "recipes", "sindri");
        _ = Directory.CreateDirectory(staged);

        Assert.Empty(RecipeResources(staged, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public void A_staged_directory_is_embedded_by_relative_path_without_the_readme()
    {
        using var home = new TempHome();
        var staged = Path.Combine(home.Path, "recipes", "sindri");
        Write(staged, "workflow.line.yaml");
        Write(staged, "README.md");
        Write(staged, "evals/workflow.line/happy.jsonl");
        Write(staged, "evals/workflow.line/deep/nested.jsonl");

        Assert.Equal(
            [
                Prefix + "evals/workflow.line/deep/nested.jsonl",
                Prefix + "evals/workflow.line/happy.jsonl",
                Prefix + "workflow.line.yaml",
            ],
            RecipeResources(staged, TestContext.Current.CancellationToken));
    }

    private static void Write(string root, string relative)
    {
        var path = Path.Combine(root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x\n");
    }

    private static string[] RecipeResources(string? stagedDir, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(Path.Combine(RepoRoot(), "src", "Sindri", "Aw.Sindri.Host", "Aw.Sindri.Host.csproj"));
        start.ArgumentList.Add("-getItem:EmbeddedResource");
        if (stagedDir is not null)
        {
            start.ArgumentList.Add("-p:AwRecipesDir=" + stagedDir);
        }

        using var process = Process.Start(start)!;   // why: the program is dotnet, which evaluates the project
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        var error = process.StandardError.ReadToEndAsync(cancellation);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output.Result + error.Result);

        using var document = JsonDocument.Parse(output.Result);
        return [.. document.RootElement.GetProperty("Items").GetProperty("EmbeddedResource").EnumerateArray()
            .Select(item => item.TryGetProperty("LogicalName", out var name) ? name.GetString() ?? "" : "")
            .Where(name => name.StartsWith("aw-recipes/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
