using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

public sealed class DesignDocRealSpecTests
{
    private const string SpecPath = "docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md";

    [Fact, Trait("Lane", "unit")]
    public void The_PR_review_page_spec_itemizes_its_phases_and_test_rows_without_errors()
    {
        var path = Path.Combine(RepoRoot(), SpecPath);

        var doc = DesignDocParser.Parse(SpecPath, File.ReadAllText(path));

        Assert.Empty(doc.Errors);
        Assert.Equal("sindri-pr-review-page", doc.FrontMatter!.Slug);
        var phases = doc.Items.Where(static i => i.Kind == DesignItemKind.Phase).Select(static i => i.Key).ToHashSet(StringComparer.Ordinal);
        for (var n = 0; n <= 10; n++)
        {
            Assert.Contains("Ph" + n.ToString(System.Globalization.CultureInfo.InvariantCulture), phases);
        }

        Assert.Contains("Ph4a", phases);
        var tests = doc.Items.Where(static i => i.Kind == DesignItemKind.TestPlan).Select(static i => i.Key).ToHashSet(StringComparer.Ordinal);
        for (var n = 1; n <= 12; n++)
        {
            Assert.Contains("TP-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture), tests);
        }

        Assert.Equal(13, doc.Items.Count(static i => i.Kind == DesignItemKind.Requirement));
        var tp1 = doc.Refs.Where(static r => r.From == "TP-1" && r.Kind == DesignRefKind.Verifies).Select(static r => r.To).ToArray();
        Assert.Equal(["REQ-7", "REQ-12"], tp1);
        Assert.Equal(1, doc.Items.Count(static i => i.Key == "Ph1"));
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
