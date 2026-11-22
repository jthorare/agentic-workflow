using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Sindri.Features.CodeIndexJobs;
using RepoStatus = Aw.CodeIndex.Core.Facade.RepoStatus;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The code index doctor's fix text per layer code (#3097): 008 has its own, 006 names the embed's own retry, and a code with no text points at the JSON report, never at setup.</summary>
public sealed class CodeIndexDoctorFixTextTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");

    private static string? FixOn(string row, LayerName layer, string code)
    {
        var down = new LayerStatus(layer, LayerState.Unavailable, code, "down");
        var status = new RepoStatus(_repo, IndexState.Ok, CommitSha.From(new string('1', 40)), _now.AddMinutes(-3), [new LayerStatus(LayerName.Structure, LayerState.Ok, Code: null, "ok"), down], new IndexCounts(Files: 1, Symbols: 2, Edges: 3));
        return CodeIndexDoctor.Rows(new([status], [], []), _now).Single(c => c.Name == row).Fix;
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unavailable_graph_008_names_graphify_and_its_install_not_the_pin()
    {
        var fix = FixOn("code-index:graphify", LayerName.Graph, "SND-CODEIDX-008");

        Assert.Contains("graphify", fix, StringComparison.Ordinal);
        Assert.Contains("scripts/install-deps.sh", fix, StringComparison.Ordinal);
        Assert.DoesNotContain("installs the pin", fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_embedding_server_failure_006_says_the_embed_retries_on_its_own()
    {
        var fix = FixOn("code-index:ollama", LayerName.Embeddings, "SND-CODEIDX-006");

        Assert.Contains("start Ollama", fix, StringComparison.Ordinal);
        Assert.Contains("retries on its own", fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_mismatch_009_keeps_the_setup_reinstall_fix()
    {
        var fix = FixOn("code-index:ollama", LayerName.Embeddings, "SND-CODEIDX-009");

        Assert.Equal("./setup.sh re-installs the pin", fix);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_code_with_no_fix_text_points_at_the_json_report_not_at_setup()
    {
        var fix = FixOn("code-index:graphify", LayerName.Graph, "SND-CODEIDX-099");

        Assert.Equal("see sindri doctor --json", fix);
    }
}
