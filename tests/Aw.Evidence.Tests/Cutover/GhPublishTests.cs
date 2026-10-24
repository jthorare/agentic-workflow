using Aw.Evidence.Core;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Features.GhPublish;
using Aw.Evidence.Testing;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover gap G-b (spec §3.3, decision Q7): gh-publish, the TypeScript verb of the same name. Red until unit 1.2 builds
/// the service over IGhRunner. Exit mapping (0 pushed, 1 refused or bad input, 3 review pending, 4 gh failed) is pinned in
/// CommandRoutingTests; these pin what the service does with the run and the repository.
/// </summary>
public sealed class GhPublishTests
{
    private const string Run = "/runs/run-1";
    private const string FirstShot = "/runs/run-1/desktop-1-click.png";
    private const string SecondShot = "/runs/run-1/desktop-2-click.png";
    private const string Branch = "ui-evidence/pr-7";

    private readonly InMemoryRunFiles _files = new();
    private readonly FakeGhRunner _gh = new FakeGhRunner().WithGitData("o/n");

    private GhPublishService Service() => new(_files, _gh, TestJson.Options);

    private static GhPublishRequest Request(DbProvenance? provenance = DbProvenance.Seeded, params string[] reviewed) =>
        new(Run, "o/n", 7, provenance, reviewed);

    private static string Pinned(string commit, string path) => $"https://github.com/o/n/blob/{commit}/{path}?raw=true";

    private ValueTask<Result<GhPublishResponse>> Handle(GhPublishRequest request) => Service().Handle(request, CancellationToken.None);

    private async Task<GhPublishResponse> Published(params string[] reviewed)
    {
        Given();
        var result = await Handle(Request(DbProvenance.Seeded, reviewed.Length == 0 ? [FirstShot, SecondShot] : reviewed));
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private void Given() => _ = _files
        .With($"{Run}/summary.json", AwJson.Serialize(A.RunSummary().WithStep(StepStatus.Passed).WithStep(StepStatus.Passed).Build(), TestJson.Options))
        .With(FirstShot, [1, 1, 1])
        .With(SecondShot, [2, 2, 2]);

    [Fact, Trait("Lane", "unit")]
    public async Task A_script_run_pushes_each_step_screenshot_to_the_PRs_evidence_branch_under_pr_n_run_id()
    {
        var response = await Published();

        Assert.Equal((Branch, _gh.Head(Branch)), (response.Branch, response.Commit));
        var files = _gh.FilesAt(response.Commit!);
        Assert.Equal([1, 1, 1], files["pr-7/run-1/desktop-1-click.png"]);
        Assert.Equal([2, 2, 2], files["pr-7/run-1/desktop-2-click.png"]);
        Assert.Equal("ui-evidence: PR #7 run run-1", _gh.MessageOf(response.Commit!));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_image_gets_a_commit_pinned_raw_url_that_renders_for_anyone_who_can_read_the_repo()
    {
        var response = await Published();

        Assert.Equal(Pinned(response.Commit!, "pr-7/run-1/desktop-1-click.png"), response.Urls[FirstShot]);
        Assert.Equal(Pinned(response.Commit!, "pr-7/run-1/desktop-2-click.png"), response.Urls[SecondShot]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_run_goes_on_top_of_the_earlier_one_and_is_never_force_pushed()
    {
        var earlier = _gh.Seed(Branch, new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["pr-7/run-0/old.png"] = [9] });

        var response = await Published();

        Assert.Equal([earlier], _gh.ParentsOf(response.Commit!));
        Assert.Equal(["pr-7/run-0/old.png", "pr-7/run-1/desktop-1-click.png", "pr-7/run-1/desktop-2-click.png"], [.. _gh.FilesAt(response.Commit!).Keys.Order(StringComparer.Ordinal)]);
        Assert.DoesNotContain(_gh.Calls, static c => c.Stdin is { } body && body.Contains("\"force\":true", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task It_writes_pr_comment_md_next_to_the_run_embedding_the_urls_and_prints_the_post_argv()
    {
        var response = await Published();

        Assert.Equal($"{Run}/pr-comment.md", response.CommentFile);
        var comment = System.Text.Encoding.UTF8.GetString(_files.Files[$"{Run}/pr-comment.md"]);
        Assert.Contains(Pinned(response.Commit!, "pr-7/run-1/desktop-1-click.png"), comment, StringComparison.Ordinal);
        Assert.Equal(["gh", "pr", "comment", "7", "--repo", "o/n", "--body-file", $"{Run}/pr-comment.md"], response.Post);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parity_run_pushes_its_side_by_side_images_and_carries_its_own_provenance()
    {
        var parity = A.ParitySummary().WithProvenance(Provenance.Seeded).WithFrame(ParitySummaryBuilder.Frame("default")).Build();
        var side = parity.Frames[0].Files.SideBySide;
        _ = _files.With("/runs/parity-1/parity.json", AwJson.Serialize(parity, TestJson.Options)).With(side, [7, 7]);

        var result = await Handle(new GhPublishRequest("/runs/parity-1", "o/n", 7, Provenance: null, [side]));

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([7, 7], _gh.FilesAt(result.Value.Commit!)["pr-7/parity-1/default-side-by-side.png"]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData(DbProvenance.Unknown)]
    public async Task A_script_run_without_seeded_provenance_pushes_nothing(DbProvenance? provenance)
    {
        Given();

        var result = await Handle(Request(provenance, FirstShot, SecondShot));

        Assert.Equal(EvidenceErrors.PublishRefused.Code, result.Error.Code);
        Assert.Empty(_gh.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_on_a_non_local_host_is_never_seeded_whatever_the_caller_claims()
    {
        _ = _files
            .With($"{Run}/summary.json", AwJson.Serialize(A.RunSummary().OnHost("https://pr-9.preview.example.com").Build(), TestJson.Options))
            .With(FirstShot, [1]);

        var result = await Handle(Request(DbProvenance.Seeded, FirstShot));

        Assert.Equal(EvidenceErrors.PublishRefused.Code, result.Error.Code);
        Assert.Empty(_gh.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scrubbed_parity_run_keeps_its_images_off_github()
    {
        var parity = A.ParitySummary().WithProvenance(Provenance.Scrubbed).WithFrame(ParitySummaryBuilder.Frame("default")).Build();
        _ = _files.With("/runs/parity-1/parity.json", AwJson.Serialize(parity, TestJson.Options)).With(parity.Frames[0].Files.SideBySide, [7]);

        var result = await Handle(new GhPublishRequest("/runs/parity-1", "o/n", 7, Provenance: null, [parity.Frames[0].Files.SideBySide]));

        Assert.Equal(EvidenceErrors.PublishRefused.Code, result.Error.Code);
        Assert.Empty(_gh.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(1)]
    public async Task An_image_that_is_not_in_the_reviewed_list_blocks_the_publish_and_nothing_is_pushed(int reviewedCount)
    {
        Given();

        var result = await Handle(Request(DbProvenance.Seeded, reviewedCount == 0 ? [] : [FirstShot]));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ReviewBlocked);
        Assert.Equal(reviewedCount == 0 ? [FirstShot, SecondShot] : [SecondShot], result.Value.Unreviewed);
        Assert.Null(result.Value.Commit);
        Assert.Empty(_gh.Calls);
        Assert.False(_files.Files.ContainsKey($"{Run}/pr-comment.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gh_failure_is_AW_EVIDENCE_025_and_no_comment_is_written()
    {
        Given();
        _gh.FailWith = "gh: Bad Gateway (HTTP 502)";

        var result = await Handle(Request(DbProvenance.Seeded, FirstShot, SecondShot));

        Assert.Equal(EvidenceErrors.GhPublishFailed.Code, result.Error.Code);
        Assert.Contains("HTTP 502", result.Error.Message, StringComparison.Ordinal);
        Assert.False(_files.Files.ContainsKey($"{Run}/pr-comment.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gh_that_would_not_start_exits_the_run_errored_way()
    {
        Given();
        _gh.WontStart = true;

        var result = await Handle(Request(DbProvenance.Seeded, FirstShot, SecondShot));

        Assert.Equal(ErrorKind.Unavailable, result.Error.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_with_no_images_is_an_error_not_an_empty_commit()
    {
        _ = _files.With($"{Run}/summary.json", AwJson.Serialize(A.RunSummary().Build(), TestJson.Options));   // its one screenshot is not on disk

        var result = await Handle(Request(DbProvenance.Seeded));

        Assert.True(result.IsError);
        Assert.Empty(_gh.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("o", 7, "/runs/run-1")]
    [InlineData("o/n/x", 7, "/runs/run-1")]
    [InlineData("../n", 7, "/runs/run-1")]
    [InlineData("o/n", 0, "/runs/run-1")]
    [InlineData("o/n", 7, "runs/run-1")]
    public void The_validator_refuses_a_repo_that_is_not_owner_name_a_pr_below_1_and_a_relative_run_directory(string repo, int pr, string runDirectory)
    {
        var result = new GhPublishValidator().Validate(new GhPublishRequest(runDirectory, repo, pr, DbProvenance.Seeded, []));

        Assert.Equal(EvidenceErrors.Invalid.Code, result.Error.Code);
    }
}
