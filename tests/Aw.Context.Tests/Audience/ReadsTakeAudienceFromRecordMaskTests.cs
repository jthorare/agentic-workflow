using System.Text.RegularExpressions;

namespace Aw.Context.Tests.Audience;

/// <summary>Only Core's RecordMask builds the audience set a Context read filters by (#2177): a read that builds its own drifts from the mask, as the issue and project reads did when they lost a human's extras.</summary>
[Trait("Lane", "unit")]
public sealed class ReadsTakeAudienceFromRecordMaskTests
{
    [Fact]
    public void No_feature_constructs_a_ProfileAudiencePolicy()
    {
        var features = Path.Combine(RepoRoot(), "src", "Context", "Aw.Context.Features");
        var hits = Directory.EnumerateFiles(features, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(static f => File.ReadLines(f).Select((line, i) => (f, line, n: i + 1)))
            .Where(static t => t.line.Contains("new ProfileAudiencePolicy(", StringComparison.Ordinal) || _targetTyped.IsMatch(t.line))
            .Select(static t => t.f + ":" + t.n.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.True(hits.Count == 0, "build the allowed set with RecordMask.Allowed, not a ProfileAudiencePolicy of your own (`new ProfileAudiencePolicy(` or the target-typed `ProfileAudiencePolicy p = new(`): " + string.Join(", ", hits));
    }

    private static readonly Regex _targetTyped = new(@"ProfileAudiencePolicy\s+\w+\s*=\s*new\s*\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above " + AppContext.BaseDirectory);
    }
}
