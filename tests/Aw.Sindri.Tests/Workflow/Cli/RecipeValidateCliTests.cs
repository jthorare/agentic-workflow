using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri recipe validate' and 'recipe eval' through the recipe command table, against recipe files on disk: exit codes
/// (W27), stdout and stderr pinned by golden files under Workflow/Cli/Golden. Re-pin them after an intended change with
/// AW_GOLDEN_UPDATE=1 (docs/csharp/sindri-workflow.md), then review the diff.
/// </summary>
public sealed class RecipeValidateCliTests : IDisposable
{
    private readonly TempHome _work = new();
    private readonly TempHome _home = new();

    public void Dispose()
    {
        _work.Dispose();
        _home.Dispose();
    }

    private Normalization Normal => new(Normalization.TempPath(_work.Path));

    [Fact, Trait("Lane", "integration")]
    public async Task An_unreachable_state_prints_its_signal_line_and_exits_1()
    {
        var file = Put("RCP-REACH-001.yaml", RecipeFixtures.Read("invalid/RCP-REACH-001.yaml"));

        var run = await Run("validate", file);

        Assert.Equal(1, run.Exit);
        Assert.StartsWith(
            "RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it\n",
            run.Stdout, StringComparison.Ordinal);
        Match(run, "validate-reach-001.txt");
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("workflow.step")]
    [InlineData("workflow.item")]
    public async Task A_shipped_recipe_passes_and_prints_its_hash(string recipe)
    {
        var file = PutShipped(recipe);

        var run = await Run("validate", file);

        Assert.Equal(0, run.Exit);
        Assert.Matches("(?m)^hash sha256:[0-9a-f]{64}\n\\z", run.Stdout);
        Match(run, $"validate-{recipe}.txt");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Workflow_item_shows_its_resource_information_and_its_skipped_effect_check()
    {
        var run = await Run("validate", PutShipped("workflow.item"));

        Assert.Contains("\ninformation: RCP-RES-001: resource 'slot' has no allocator in this build", "\n" + run.Stdout, StringComparison.Ordinal);
        Assert.Contains("\nskipped: RCP-EFFECT-001: effect 'effect.claim.ref'", "\n" + run.Stdout, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_multi_fault_recipe_lists_errors_first_in_ascii_and_exits_1()
    {
        var file = Put("multi-fault.yaml", await File.ReadAllTextAsync(
            Golden.SourcePath(typeof(RecipeValidateCliTests).Assembly, "Workflow/Cli/Inputs/multi-fault.yaml"), TestContext.Current.CancellationToken));

        var run = await Run("validate", file);

        Assert.Equal(1, run.Exit);
        Assert.All(run.Stdout, c => Assert.InRange(c, '\n', '~'));
        var kinds = run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Kind).ToList();
        Assert.Equal(kinds.Order(), kinds);
        Assert.True(kinds.Where(k => k == 0).Skip(2).Any(), run.Stdout);
        Assert.Contains(1, kinds);
        Assert.Contains(2, kinds);
        Match(run, "validate-multi-fault.txt");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_file_exits_2_with_SND_RECIPE_010_and_its_fix()
    {
        var run = await Run("validate", Path.Combine(_work.Path, "missing.yaml"));

        Assert.Equal((2, string.Empty), (run.Exit, run.Stdout));
        Assert.StartsWith("SND-RECIPE-010 ", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("(fix: ", run.Stderr, StringComparison.Ordinal);
        Match(run, "validate-missing-file.txt");
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("validate", "validate-no-file.txt")]
    [InlineData("validate --json", "validate-json-no-file.txt")]
    [InlineData("", "recipe-no-verb.txt")]
    public async Task Without_a_file_it_is_a_usage_error_and_exits_2(string args, string golden)
    {
        var run = await Run(args.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal((2, string.Empty), (run.Exit, run.Stdout));
        Assert.Contains("usage: sindri recipe validate <file> [--json]", run.Stderr, StringComparison.Ordinal);
        Match(run, golden);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_for_one_fault_names_each_field_and_exits_1()
    {
        var run = await Run("validate", Put("RCP-REACH-001.yaml", RecipeFixtures.Read("invalid/RCP-REACH-001.yaml")), "--json");

        Assert.Equal(1, run.Exit);
        using var json = JsonDocument.Parse(run.Stdout);
        var finding = Assert.Single(json.RootElement.GetProperty("findings").EnumerateArray());
        Assert.Equal(
            ["code", "severity", "message", "file", "line", "column", "path", "fix"],
            finding.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.False(json.RootElement.GetProperty("passed").GetBoolean());
        Match(run with { Stdout = Indented(json) }, "validate-json-reach-001.txt");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_for_a_valid_recipe_carries_the_hash_and_the_normalized_long_form()
    {
        var run = await Run("validate", "--json", PutShipped("workflow.step"));

        Assert.Equal(0, run.Exit);
        Assert.Equal(1, run.Stdout.Count(c => c == '\n'));
        using var json = JsonDocument.Parse(run.Stdout);
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("normalized").ValueKind);
        Assert.StartsWith("sha256:", json.RootElement.GetProperty("hash").GetString(), StringComparison.Ordinal);
        Match(run with { Stdout = Indented(json) }, "validate-json-workflow.step.txt");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bug_inside_validation_is_a_tool_error_with_exit_2_not_a_crash()
    {
        var file = PutShipped("workflow.step");

        var run = await RunWith(static services => services.AddSingleton<IRecipeReader, ThrowingReader>(), "validate", file);

        Assert.Equal((2, string.Empty), (run.Exit, run.Stdout));
        Assert.StartsWith("SND-API-005 recipe validate failed inside sindri: InvalidOperationException: the reader broke", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("(fix: ", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Cancellation_passes_through_the_tool_error_boundary()
    {
        var file = PutShipped("workflow.step");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => RunWith(static services => services.AddSingleton<IRecipeReader, CancelledReader>(), "validate", file));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Eval_says_it_arrives_with_P3_and_exits_2()
    {
        var run = await Run("eval", PutShipped("workflow.step"));

        Assert.Equal((2, string.Empty), (run.Exit, run.Stdout));
        Match(run, "eval-not-yet.txt");
    }

    private static int Kind(string line) =>
        line.StartsWith("information: ", StringComparison.Ordinal) ? 1
        : line.StartsWith("skipped: ", StringComparison.Ordinal) ? 2
        : line.StartsWith("hash ", StringComparison.Ordinal) ? 3
        : 0;

    private string Put(string name, string text)
    {
        var path = Path.Combine(_work.Path, name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Puts both shipped recipes beside each other, as in recipes/sindri, so workflow.item finds its child.</summary>
    private string PutShipped(string recipe)
    {
        foreach (var name in new[] { "workflow.step", "workflow.item" })
        {
            _ = Put(name + ".yaml", ShippedRecipes.Read($"sindri/{name}.yaml"));
        }

        return Path.Combine(_work.Path, recipe + ".yaml");
    }

    private void Match(CliRun run, string golden) =>
        Golden.Match(run.Transcript, typeof(RecipeValidateCliTests).Assembly, "Workflow/Cli/Golden/" + golden, Normal);

    private static string Indented(JsonDocument json)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NewLine = "\n" }))
        {
            json.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Runs 'sindri recipe &lt;args&gt;' in-process against a throwaway home, and checks it wrote nothing there.</summary>
    private Task<CliRun> Run(params string[] args) => RunWith(static _ => { }, args);

    private async Task<CliRun> RunWith(Action<IServiceCollection> replace, params string[] args)
    {
        var collection = Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider());
        replace(collection);
        var provider = collection.BuildServiceProvider();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new Output(stdout, stderr, services.GetRequiredService<JsonSerializerOptions>());

        var exit = await RecipeCommandTable.Run(args, services, output, TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_home.Path));
        return new CliRun(string.Join(' ', ["sindri", "recipe", .. args]), exit, stdout.ToString(), stderr.ToString());
    }

    private sealed class ThrowingReader : IRecipeReader
    {
        public RecipeReading Read(string text, string file) => throw new InvalidOperationException("the reader broke");
    }

    private sealed class CancelledReader : IRecipeReader
    {
        public RecipeReading Read(string text, string file) => throw new OperationCanceledException("cancelled");
    }

    private sealed record CliRun(string Command, int Exit, string Stdout, string Stderr)
    {
        public string Transcript => string.Create(CultureInfo.InvariantCulture, $"$ {Command}\nexit {Exit}\n--- stdout\n{Stdout}--- stderr\n{Stderr}");
    }
}
