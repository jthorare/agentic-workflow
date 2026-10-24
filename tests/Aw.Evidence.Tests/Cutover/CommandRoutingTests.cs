using Aw.Evidence.Core;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Features.Doctor;
using Aw.Evidence.Features.GhPublish;
using Aw.Evidence.Tests.Cli;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover §3.2, the verbs the skill calls: every one is a verb of the command table, takes the flags the skill's command lines
/// carry, and the new ones (gh-publish, version) are routed with the exit codes the skill documents. The service behind
/// gh-publish is a stub until unit 1.2, so the exit mapping is pinned over a scripted service.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(ConsoleCapture.Name)]
public sealed class CommandRoutingTests : IDisposable
{
    private readonly CutoverCli _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task The_usage_line_lists_every_verb_the_skill_calls()
    {
        var (exit, output, error) = await _cli.Run();

        Assert.Equal((1, ""), (exit, output));
        var verbs = error[(error.IndexOf('<', StringComparison.Ordinal) + 1)..error.IndexOf('>', StringComparison.Ordinal)].Split('|');
        foreach (var verb in new[]
        {
            "run", "parity", "parity-plan", "parity-comment", "parity-stale", "parity-put", "publish-plan", "doctor", "provenance",
            "install-browser", "api-run", "db-run", "desktop-run", "gh-publish", "version",
        })
        {
            Assert.Contains(verb, verbs, StringComparer.Ordinal);
        }
    }

    // §3.2 rows 3, 7, 8, 9, 10: the flags of the skill's command lines bind (the run itself fails later on the missing inputs, which is not a usage error).
    [Theory]
    [InlineData("run", "s.json", "out", "--baseline", "b.png", "--app-build", "abc", "--fixtures", "f1", "--cache", "c.json", "--host", "http://localhost:3000", "--allow-preview-host", "--allow-writes", "--lock-timeout", "5")]
    [InlineData("parity", "m.json", "out", "--host", "http://localhost:3000", "--allow-preview-host", "--allow-writes", "--app-build", "abc", "--db-provenance", "seeded", "--lock-timeout", "5")]
    [InlineData("parity-plan", "out", "--reviewed", "a.png,b.png")]
    [InlineData("parity-comment", "out", "--for", "github")]
    [InlineData("parity-stale", "--attachments", "a.json", "--keep", "id1,id2")]
    [InlineData("parity-put", "--upload", "u.json", "--file", "a.png")]
    [InlineData("publish-plan", "out", "--provenance", "seeded", "--linear-issue", "ABC-1")]
    [InlineData("api-run", "s.json", "out", "--at", "base", "--repo", ".", "--host", "http://localhost:3000", "--app-build", "abc", "--config", "c.json", "--lock-timeout", "5")]
    [InlineData("db-run", "s.json", "out", "--at", "head", "--repo", ".", "--app-build", "abc", "--config", "c.json", "--lock-timeout", "5")]
    [InlineData("desktop-run", "s.json", "out", "--repo", ".", "--app-build", "abc", "--config", "c.json", "--screens", "home,agents", "--lock-timeout", "5")]
    public async Task The_flags_of_a_skill_command_line_bind(params string[] args)
    {
        string[] files = ["s.json", "m.json", "out", "u.json", "a.json"];
        var (_, _, error) = await _cli.Run([args[0], .. args[1..].Select(a => files.Contains(a, StringComparer.Ordinal) ? _cli.Root.Combine(a) : a)]);

        Assert.DoesNotContain("usage: ui-evidence", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("out")]
    [InlineData("out", "--repo", "o/n")]
    [InlineData("out", "--pr", "7")]
    [InlineData("out", "--repo", "o/n", "--pr", "0")]
    [InlineData("out", "--repo", "o/n", "--pr", "seven")]
    [InlineData("out", "--repo", "o/n", "--pr", "-1")]
    [InlineData("out", "--repo", "o/n", "--pr", "7", "--provenance", "maybe")]
    [InlineData("out", "--repo", "o/n", "--pr", "7", "--db-provenance", "seeded")]   // one flag name: --provenance
    [InlineData("out", "--repo", "o/n", "--pr", "7", "extra")]
    public async Task Gh_publish_without_its_arguments_is_a_usage_error(params string[] args)
    {
        var (exit, output, error) = await _cli.Run(["gh-publish", .. args]);

        Assert.Equal((1, ""), (exit, output));
        Assert.StartsWith("ui-evidence: usage: ui-evidence gh-publish <run-dir> --repo owner/name --pr N", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gh_publish_binds_the_run_the_repo_the_pr_the_provenance_and_the_reviewed_images()
    {
        var service = new ScriptedService<GhPublishRequest, GhPublishResponse>(_ => Pushed());
        _cli.Configure = services => services.Replace(ServiceDescriptor.Scoped<IService<GhPublishRequest, GhPublishResponse>>(_ => service));
        var run = _cli.Root.Combine("run-1");

        var (exit, _, _) = await _cli.Run("gh-publish", run, "--repo", "o/n", "--pr", "7", "--provenance", "seeded", "--reviewed", $"{run}/a.png,{run}/b.png");

        Assert.Equal(0, exit);
        var request = Assert.Single(service.Requests);
        Assert.Equal((run, "o/n", 7, DbProvenance.Seeded), (request.RunDirectory, request.Repo, request.Pr, request.Provenance));
        Assert.Equal([$"{run}/a.png", $"{run}/b.png"], request.Reviewed);
    }

    [Fact]
    public async Task Gh_publish_that_pushed_exits_0_and_prints_the_branch_the_commit_the_urls_the_comment_file_and_the_post_argv()
    {
        _cli.Configure = services => services.Replace(ServiceDescriptor.Scoped<IService<GhPublishRequest, GhPublishResponse>>(_ => new ScriptedService<GhPublishRequest, GhPublishResponse>(_ => Pushed())));

        var (exit, output, _) = await _cli.Run("gh-publish", _cli.Root.Combine("run-1"), "--repo", "o/n", "--pr", "7", "--provenance", "seeded");

        Assert.Equal(0, exit);
        Assert.Contains("\"branch\":\"ui-evidence/pr-7\"", output, StringComparison.Ordinal);
        Assert.Contains("\"commit\":\"abc123\"", output, StringComparison.Ordinal);
        Assert.Contains("\"commentFile\":\"/runs/run-1/pr-comment.md\"", output, StringComparison.Ordinal);
        Assert.Contains("\"post\":[\"gh\",\"pr\",\"comment\",\"7\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gh_publish_blocked_on_review_exits_3_and_lists_the_unreviewed_images()
    {
        var blocked = new GhPublishResponse(
            Provenance.Seeded, ReviewBlocked: true, Unreviewed: ["/runs/run-1/a.png"], Branch: null, Commit: null, Urls: new Dictionary<string, string>(StringComparer.Ordinal), CommentFile: null, Post: []);
        _cli.Configure = services => services.Replace(ServiceDescriptor.Scoped<IService<GhPublishRequest, GhPublishResponse>>(_ => new ScriptedService<GhPublishRequest, GhPublishResponse>(_ => blocked)));

        var (exit, output, _) = await _cli.Run("gh-publish", _cli.Root.Combine("run-1"), "--repo", "o/n", "--pr", "7", "--provenance", "seeded");

        Assert.Equal(3, exit);
        Assert.Contains("\"reviewBlocked\":true", output, StringComparison.Ordinal);
        Assert.Contains("\"unreviewed\":[\"/runs/run-1/a.png\"]", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("publish-refused", 1)]
    [InlineData("gh-failed", 4)]
    [InlineData("invalid", 1)]
    public async Task Gh_publish_that_refused_exits_1_and_one_that_gh_failed_exits_4(string kind, int expected)
    {
        var error = kind switch
        {
            "publish-refused" => EvidenceErrors.PublishRefused.With("provenance unknown: images stay off GitHub (seeded data only)"),
            "gh-failed" => EvidenceErrors.GhPublishFailed.With("gh api git/blobs failed: HTTP 502"),
            _ => EvidenceErrors.Invalid.With("--repo must be owner/name"),
        };
        _cli.Configure = services => services.Replace(ServiceDescriptor.Scoped<IService<GhPublishRequest, GhPublishResponse>>(_ => new ScriptedService<GhPublishRequest, GhPublishResponse>(_ => error)));

        var (exit, output, stderr) = await _cli.Run("gh-publish", _cli.Root.Combine("run-1"), "--repo", "o/n", "--pr", "7", "--provenance", "seeded");

        Assert.Equal((expected, ""), (exit, output));
        Assert.StartsWith($"ui-evidence: {error.Code} ", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_is_a_routed_verb_that_takes_no_arguments()
    {
        var (exit, output, error) = await _cli.Run("version", "now");

        Assert.Equal((1, ""), (exit, output));
        Assert.StartsWith("ui-evidence: usage: ui-evidence version", error, StringComparison.Ordinal);
        Assert.DoesNotContain("unknown command", (await _cli.Run("version")).Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Doctor_prints_the_slot_host_and_database_it_checked_in_its_json()
    {
        var slot = new DoctorSlot(2, "/wt", "http://localhost:3020", "app_2");
        _cli.Configure = services => services.Replace(ServiceDescriptor.Scoped<IService<DoctorRequest, DoctorResponse>>(_ => new ScriptedService<DoctorRequest, DoctorResponse>(_ => new DoctorResponse(Healthy: true, Failures: [], Slot: slot))));

        var (exit, output, _) = await _cli.Run("doctor");

        Assert.Equal(0, exit);
        Assert.Contains("\"slot\":{\"number\":2,\"worktree\":\"/wt\",\"host\":\"http://localhost:3020\",\"database\":\"app_2\"}", output, StringComparison.Ordinal);
    }

    private static GhPublishResponse Pushed() => new(
        Provenance.Seeded, ReviewBlocked: false, [], "ui-evidence/pr-7", "abc123",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["/runs/run-1/a.png"] = "https://github.com/o/n/blob/abc123/pr-7/run-1/a.png?raw=true" },
        "/runs/run-1/pr-comment.md", ["gh", "pr", "comment", "7", "--repo", "o/n", "--body-file", "/runs/run-1/pr-comment.md"]);
}
