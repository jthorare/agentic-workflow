using System.Text.RegularExpressions;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Graph;

/// <summary>
/// #3944: the Ui's Add step tells a foreign example reference with copies of Core's rules, because the Ui does not reference Core: the template pattern (Template.Reference),
/// the scope roots (GraphValidator._scopeRoots) and the condition keywords (Tokenizer.Word). This test reads the sources as text from the repository, so a change in Core
/// fails here until the copy in src/Sindri/Aw.Sindri.Ui/ViewModels/Workflows/ExampleReferences*.cs follows.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class ExampleReferencesCopyTests
{
    private const string Ui = "src/Sindri/Aw.Sindri.Ui/ViewModels/Workflows/";

    [Fact]
    public void The_Ui_reads_template_references_with_Cores_pattern() =>
        Assert.Equal(PatternOf("src/Sindri/Aw.Sindri.Core/Graph/Interpolation/Template.cs"), PatternOf(Ui + "ExampleReferences.cs"));

    [Fact]
    public void The_Ui_knows_Cores_scope_roots() =>
        Assert.Equal(ScopeRootsOf("src/Sindri/Aw.Sindri.Core/Graph/Validation/GraphValidator.References.cs"), ScopeRootsOf(Ui + "ExampleReferences.cs"));

    [Fact]
    public void The_Ui_skips_the_words_Cores_condition_reads_as_keywords()
    {
        var core = CoreKeyword.Matches(Source("src/Sindri/Aw.Sindri.Core/Graph/Expressions/Tokenizer.cs")).Select(static m => m.Groups["word"].Value).Order(StringComparer.Ordinal).ToList();
        var list = UiKeywords.Match(Source(Ui + "ExampleReferences.Condition.cs"));
        Assert.True(list.Success, "no _keywords list in the Ui's ExampleReferences.Condition.cs");
        var ui = Quoted.Matches(list.Groups["words"].Value).Select(static m => m.Groups["word"].Value).Order(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(core);
        Assert.Equal(core, ui);
    }

    private static string PatternOf(string path)
    {
        var match = ReferencePattern.Match(Source(path));
        Assert.True(match.Success, $"no [GeneratedRegex] on a Reference property in {path}");
        return match.Groups["attr"].Value;
    }

    private static string ScopeRootsOf(string path)
    {
        var match = ScopeRoots.Match(Source(path));
        Assert.True(match.Success, $"no _scopeRoots list in {path}");
        return match.Groups["roots"].Value;
    }

    private static string Source(string path) => File.ReadAllText(Path.Combine(RepoRoot(), path));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"(?<attr>\[GeneratedRegex\(.*\)\])\r?\n\s*private static partial Regex Reference\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferencePattern { get; }

    [GeneratedRegex(@"_scopeRoots = new\(\[(?<roots>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScopeRoots { get; }

    [GeneratedRegex(@"""(?<word>[a-z]+)"" => Kind\.(And|Or|Not|True|False|Null)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CoreKeyword { get; }

    [GeneratedRegex(@"_keywords = new\(\[(?<words>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UiKeywords { get; }

    [GeneratedRegex(@"""(?<word>[a-z]+)""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted { get; }
}
