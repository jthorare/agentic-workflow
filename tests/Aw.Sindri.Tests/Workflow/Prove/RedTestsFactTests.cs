using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>'fact:redTests' has one parser (line spec A.7): an object keyed by item id, each value a list of test ids or {tests?, appOnly?, commit?, output?}.</summary>
[Lane(TestLane.Unit)]
public sealed class RedTestsFactTests
{
    // What the red agent of run24 wrote, because nothing told it the shape.
    private const string Run24 = """{"commit":"eb60275","file":"scripts/tests/binaries-lock.test.sh","command":"bash x.sh","result":{"exit":1},"tests":[{"name":"list prints","covers":["AC-1"],"status":"failing","why":"exit 2"}]}""";

    private static readonly string[] _topLevel = ["commit", "file", "command", "tests"];

    private static RedTestsFact Parse(string json) => RedTestsFact.Parse(JsonNode.Parse(json));

    [Fact]
    public void The_canonical_shape_gives_each_item_its_tests_paths_and_commit()
    {
        var fact = Parse("""{"AC-1":{"tests":[{"id":"Fix_works","path":"tests/FixTests.cs"},"Other"],"commit":"abc1234","output":"1 failed"},"AC-2":{"appOnly":{"signal":"SG-1"}}}""");

        Assert.Null(fact.Problem);
        var first = fact.Entries[0];
        Assert.Equal(("AC-1", null), (first.Item, first.Problem));
        Assert.Equal([new RedTest("Fix_works", "tests/FixTests.cs"), new RedTest("Other", Path: null)], first.Tests);
        Assert.Equal("abc1234", first.Commit);
        Assert.True(fact.Entries[1].AppOnly);
        Assert.Empty(fact.Entries[1].Tests);
    }

    [Fact]
    public void The_short_form_a_list_of_ids_is_the_item_s_tests()
    {
        var fact = Parse("""{"i1":["T1","T2"]}""");

        Assert.Equal(["T1", "T2"], fact.Entries.Single().Tests.Select(static t => t.Name), StringComparer.Ordinal);
        Assert.Null(fact.Entries.Single().Problem);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public void An_empty_fact_holds_no_entry_and_no_problem(string json)
    {
        var fact = Parse(json);

        Assert.Empty(fact.Entries);
        Assert.Null(fact.Problem);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"prose\"")]
    public void A_fact_that_is_not_an_object_is_a_problem_stating_the_shape(string json)
    {
        var fact = Parse(json);

        Assert.Contains(RedTestsFact.Shape, fact.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Run24_s_fact_is_a_problem_per_wrong_field_naming_it_and_the_shape_and_is_never_read_as_tests()
    {
        var fact = Parse(Run24);

        var problems = fact.Entries.Where(static e => e.Problem is not null).ToList();
        Assert.Equal(["commit", "file", "command", "tests"], problems.Select(static e => e.Item).Where(static i => _topLevel.Contains(i, StringComparer.Ordinal)), StringComparer.Ordinal);
        Assert.All(problems, static e =>
        {
            Assert.Contains(e.Item, e.Problem, StringComparison.Ordinal);
            Assert.Contains(RedTestsFact.Shape, e.Problem, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(fact.Entries, static e => e.Tests.Count > 0);
    }

    [Theory]
    [InlineData("""{"A1":["T1",7]}""")]
    [InlineData("""{"A1":{"tests":"T1"}}""")]
    [InlineData("""{"A1":{"tests":[{"name":"x"}]}}""")]
    [InlineData("""{"A1":{"appOnly":"the page shows it"}}""")]
    [InlineData("""{"A1":{"tests":["T1"],"commit":5}}""")]
    public void A_value_of_another_shape_is_a_problem_naming_the_item(string json)
    {
        var entry = Parse(json).Entries.Single();

        Assert.Contains("A1", entry.Problem, StringComparison.Ordinal);
        Assert.Contains(RedTestsFact.Shape, entry.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void The_red_commit_is_the_one_every_item_that_names_one_agrees_on()
    {
        var (commit, problem) = Parse("""{"A1":{"tests":["T1"],"commit":"h1"},"A2":{"tests":["T2"],"commit":"h1"},"A3":["T3"]}""").RedCommit();

        Assert.Equal(("h1", null), (commit, problem));
    }

    [Fact]
    public void Items_naming_different_commits_are_a_problem_naming_both()
    {
        var (commit, problem) = Parse("""{"A1":{"tests":["T1"],"commit":"h1"},"A2":{"tests":["T2"],"commit":"h2"}}""").RedCommit();

        Assert.Null(commit);
        Assert.Contains("h1", problem, StringComparison.Ordinal);
        Assert.Contains("h2", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void No_item_naming_a_commit_is_a_problem_saying_where_it_goes()
    {
        var (commit, problem) = Parse("""{"A1":["T1"]}""").RedCommit();

        Assert.Null(commit);
        Assert.Contains("commit", problem, StringComparison.Ordinal);
        Assert.Contains(RedTestsFact.Shape, problem, StringComparison.Ordinal);
    }
}
