using System.Diagnostics;
using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Infrastructure.Embedding;
using Aw.CodeIndex.Infrastructure.Graph;
using Aw.CodeIndex.Infrastructure.Scrubbing;

namespace Aw.CodeIndex.Tests.Build;

// The owner's proof for #990, opt-in: a real repo built and embedded at one commit, then built and embedded again at a later one, over the real
// git, graphify and Ollama. The second embed must make vectors only for the symbols of the files that changed and carry every other one.
// AW_CODEINDEX_PROOF_REPO names the checkout, AW_CODEINDEX_PROOF_FROM and AW_CODEINDEX_PROOF_TO the two commits; it skips without them.
public sealed class IncrementalEmbedProofHeavyTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "heavy")]
    public async Task A_main_move_embeds_only_the_symbols_of_the_changed_files()
    {
        var (root, from, to) = (Env("AW_CODEINDEX_PROOF_REPO"), Env("AW_CODEINDEX_PROOF_FROM"), Env("AW_CODEINDEX_PROOF_TO"));
        Assert.SkipWhen(root is null || from is null || to is null, "set AW_CODEINDEX_PROOF_REPO, AW_CODEINDEX_PROOF_FROM and AW_CODEINDEX_PROOF_TO to run the #990 proof");
        var embedder = new OllamaEmbedder();
        var repo = new Core.Facade.CodeIndexRepo(RepoName.From(Path.GetFileName(root!)), root!, Core.CodeIndexSettings.Default);
        var probe = await embedder.Probe(repo.Settings.Embeddings, Token);
        Assert.SkipWhen(probe.IsError, "no Ollama with the pinned model on this box");
        using var index = new RealIndex(embedder, new GraphifyRunner(GraphifyOptions.FromEnvironment(Environment.GetEnvironmentVariable), new RedactingBodyScrubber()));

        var first = await Step(index, repo, from!);
        var second = await Step(index, repo, to!);
        var changed = ChangedFiles(root!, from!, to!);

        output.WriteLine($"changed code files {from}..{to}: {changed.Count}");
        Assert.Equal(0, first.Embed.Carried);
        Assert.True(second.Build.Incremental, "the second build is incremental");
        Assert.True(second.Embed.Carried > 0, "vectors are carried");
        Assert.True(second.Embed.Made < first.Embed.Made / 4, string.Create(CultureInfo.InvariantCulture, $"the second embed made {second.Embed.Made} vectors, against {first.Embed.Made} for the whole repo"));
        Assert.Equal(LayerState.Ok, second.Embed.Embeddings);
    }

    private async Task<(BuildReport Build, EmbedCodeIndexResponse Embed)> Step(RealIndex index, Core.Facade.CodeIndexRepo repo, string commit)
    {
        var watch = Stopwatch.StartNew();
        var build = (await index.Service.Handle(new BuildCodeIndexRequest(repo, CommitSha.From(commit), new Lines()), Token)).Value;
        var built = watch.ElapsedMilliseconds;
        var embed = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Lines()), Token)).Value;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{commit[..8]}: build incremental={build.Incremental} files={build.Counts.Files} symbols={build.Counts.Symbols} {built} ms; embed made={embed.Made} carried={embed.Carried} of {embed.Embeddable} state={embed.Embeddings} {watch.ElapsedMilliseconds - built} ms"));
        return (build, embed);
    }

    private static List<string> ChangedFiles(string root, string from, string to)
    {
        var start = new ProcessStartInfo("git", ["-C", root, "diff", "--name-only", from, to]) { RedirectStandardOutput = true, UseShellExecute = false };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var names = process.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(p => Core.Planning.LanguageTable.Of(p) is not null).ToList();
        process.WaitForExit();
        return names;
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
