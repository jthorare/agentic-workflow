using System.Text;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Contract;

public sealed class ResultJsonTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ContractJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void A_result_round_trips_and_writes_the_spec_names()
    {
        var at = new DateTimeOffset(2026, 10, 10, 18, 2, 11, TimeSpan.Zero);
        var cite = new Citation(RecordKey.From("repo:split-panes/docs/architecture.md"),
            [new CitationRef(SourceId.From("repo-docs"), ExternalId.From("split-panes/docs/architecture.md#L1-L9"), SourceVersion.From("3b18e51"))
                { Current = SourceVersion.From("9c0ffee") }],
            Permalink: null, Trust.Untrusted, CitationState.Stale);
        var item = new ResultItem("record", Node: null, "Architecture", "<untrusted source=\"repo-docs\">text</untrusted>", 0.016,
            ["rrf:repo-docs#1"], Provenance.Deterministic, Confirmed: false, Trust.Untrusted, ["C1"])
        { Tokens = 12 };
        var result = new ContextResult(ContextResult.CurrentSchemaVersion, QueryId.From("q_1"), new ContextQuery("split pane"), [], MapVersion: null, at,
            [new ResultSection("records", [item])], [], [],
            [new DegradedSource(SourceId.From("onyx"), "SND-CONTEXT-010", "Onyx did not answer", at)], new Dictionary<string, Citation>(StringComparer.Ordinal) { ["C1"] = cite });

        var bytes = AwJson.Serialize(result, _json);
        var text = Encoding.UTF8.GetString(bytes);
        var back = AwJson.Deserialize<ContextResult>(bytes, _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(text, Encoding.UTF8.GetString(AwJson.Serialize(back.Value, _json)));
        Assert.Contains("\"schemaVersion\":1", text, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"stale\"", text, StringComparison.Ordinal);
        Assert.Contains("\"trust\":\"untrusted\"", text, StringComparison.Ordinal);
        Assert.Contains("\"provenance\":\"deterministic\"", text, StringComparison.Ordinal);
        Assert.Contains("\"current\":\"9c0ffee\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_citations_aliases_round_trip_as_a_list()
    {
        var cite = new Citation(RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103"),
            [new CitationRef(SourceId.From("onyx"), ExternalId.From("0e5a1b00-0000-4000-8000-000000000103"), SourceVersion.From("2026-07-23T09:00:00.0000000Z"))],
            Permalink: null, Trust.Untrusted, CitationState.Unchecked)
        { Aliases = ["SPL-103"] };

        var bytes = AwJson.Serialize(cite, _json);

        Assert.Contains("\"aliases\":[\"SPL-103\"]", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(["SPL-103"], AwJson.Deserialize<Citation>(bytes, _json).Value.Aliases, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Scrub_blocked_is_written_in_kebab_case() =>
        Assert.Equal("\"scrub-blocked\"", Encoding.UTF8.GetString(AwJson.Serialize(CitationState.ScrubBlocked, _json)));

    [Fact, Trait("Lane", "unit")]
    public void Other_shapes_serialize_without_diff_or_repo()
    {
        var at = new DateTimeOffset(2026, 10, 10, 18, 2, 11, TimeSpan.Zero);
        var result = new ContextResult(ContextResult.CurrentSchemaVersion, QueryId.From("q_1"), new ContextQuery("split pane"), [], MapVersion: null, at,
            [], [], [], [], new Dictionary<string, Citation>(StringComparer.Ordinal));

        var text = Encoding.UTF8.GetString(AwJson.Serialize(result, _json));

        Assert.DoesNotContain("\"diff\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"repo\"", text, StringComparison.Ordinal);
        Assert.Contains("\"query\":{\"text\":\"split pane\",\"node\":null,\"item\":null,\"symbol\":null,\"depth\":null,\"budget\":null,\"draft\":false}", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_diff_result_writes_the_range_as_one_string_and_the_part_with_the_wire_names()
    {
        var at = new DateTimeOffset(2026, 10, 10, 18, 2, 11, TimeSpan.Zero);
        var sha = "0123456789abcdef0123456789abcdef01234567";
        var node = new DiffNode("feature:layout.split-pane", Confirmed: true);
        var part = new DiffPart(ContextRepoName.From("aw-split-panes"), sha, sha, sha,
            [new DiffFile("src/a.ts", ChangeKind.TypeChanged, DiffStatus.Mapped, [node])], [], new DiffCounts(1, 1, 0, 0, 0), Truncated: false, 1);
        var result = new ContextResult(ContextResult.CurrentSchemaVersion, QueryId.From("q_1"), new ContextQuery(Text: null, Diff: new DiffRange(sha, sha), Repo: ContextRepoName.From("aw-split-panes")), ["feature:layout.split-pane"],
            "v1", at, [], [], [], [], new Dictionary<string, Citation>(StringComparer.Ordinal), part);

        var bytes = AwJson.Serialize(result, _json);
        var text = Encoding.UTF8.GetString(bytes);
        var back = AwJson.Deserialize<ContextResult>(bytes, _json);

        Assert.Contains($"\"diff\":\"{sha}..{sha}\",\"repo\":\"aw-split-panes\"", text, StringComparison.Ordinal);
        Assert.Contains("\"change\":\"typeChanged\",\"status\":\"mapped\"", text, StringComparison.Ordinal);
        Assert.Contains("\"anchorsTotal\":1", text, StringComparison.Ordinal);
        Assert.Contains("\"mergeBase\":\"" + sha + "\"", text, StringComparison.Ordinal);
        Assert.True(back.IsSuccess);
        Assert.Equal(text, Encoding.UTF8.GetString(AwJson.Serialize(back.Value, _json)));
    }
}
