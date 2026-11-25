using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Publishing;

public sealed class ParityPublishingTests
{
    private static readonly BoxCheck _failed = new("testId:save", RelativeTo: null, new ExpectedGeometry(W: 40), new MeasuredGeometry(42, 20, 0, 0), 0, Pass: false, "w 42 != 40");
    private static readonly BoxCheck _passed = _failed with { Pass = true, Failure = null };

    [Theory, Trait("Lane", "unit")]
    [InlineData(Provenance.Seeded, true)]
    [InlineData(Provenance.Scrubbed, true)]
    [InlineData(Provenance.Unknown, false)]
    public void Linear_takes_seeded_and_scrubbed_data_only(Provenance provenance, bool allowed) =>
        Assert.Equal(allowed, PublishPolicy.CanUploadToLinear(provenance));

    [Theory, Trait("Lane", "unit")]
    [InlineData(Provenance.Seeded, true, true)]
    [InlineData(Provenance.Seeded, false, false)]
    [InlineData(Provenance.Scrubbed, true, true)]
    [InlineData(Provenance.Scrubbed, false, false)]
    [InlineData(Provenance.Unknown, true, false)]
    public void Pr_comments_link_images_for_seeded_or_scrubbed_data_and_an_approved_uploader(Provenance provenance, bool uploader, bool allowed) =>
        Assert.Equal(allowed, PublishPolicy.CanEmbedImagesInPr(provenance, uploader));

    [Fact, Trait("Lane", "unit")]
    public void The_subtitle_quotes_node_build_viewport_diff_and_known_differences()
    {
        var summary = A.ParitySummary().Verified("abc123", "file:9").WithFrame(ParitySummaryBuilder.Frame("empty", known: ["icon set", "font"])).Build();

        Assert.Equal("design node file:9 · build abc123 · viewport 1512x982 · diff 3% (3/100 px) · known differences: icon set; font",
            ParityAttachments.Subtitle(summary, summary.Frames[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_frames_own_design_node_wins_and_unknowns_are_named()
    {
        var summary = A.ParitySummary().WithFrame(ParitySummaryBuilder.Frame("a") with { DesignNode = "1:2" }).Build();

        Assert.StartsWith("design node 1:2 · build unverified", ParityAttachments.Subtitle(summary, summary.Frames[0]), StringComparison.Ordinal);
        Assert.EndsWith("no known differences", ParityAttachments.Subtitle(summary, summary.Frames[0]), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_long_subtitle_is_cut_to_500_characters_with_an_ellipsis()
    {
        var summary = A.ParitySummary().WithFrame(ParitySummaryBuilder.Frame(known: [new string('k', 600)])).Build();

        var subtitle = ParityAttachments.Subtitle(summary, summary.Frames[0]);

        Assert.Equal(500, subtitle.Length);
        Assert.EndsWith("…", subtitle, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_plan_has_one_side_by_side_attachment_per_frame()
    {
        var summary = A.ParitySummary().WithFrame(ParitySummaryBuilder.Frame("a")).WithFrame(ParitySummaryBuilder.Frame("b", diffPixels: 0)).Build();

        var plan = ParityAttachments.Plan(summary, static _ => 2048);

        Assert.Equal(["a", "b"], plan.Select(static p => p.Frame), StringComparer.Ordinal);
        Assert.Equal("/runs/parity-1/a-side-by-side.png", plan[0].File);
        Assert.Equal("a-side-by-side.png", plan[0].Filename);
        Assert.Equal(("image/png", 2048L, "Pixel diff: a (Figma | implementation | diff)"), (plan[0].ContentType, plan[0].Size, plan[0].Title));
        Assert.Contains("0% differing", plan[1].Expectation, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Files_not_confirmed_as_reviewed_block_the_upload()
    {
        var plan = ParityAttachments.Plan(A.ParitySummary().WithFrame(ParitySummaryBuilder.Frame("a")).WithFrame(ParitySummaryBuilder.Frame("b")).Build(), static _ => 1);

        Assert.Equal(["/runs/parity-1/b-side-by-side.png"], ParityAttachments.Unreviewed(plan, ["/runs/parity-1/a-side-by-side.png"]));
        Assert.Empty(ParityAttachments.Unreviewed(plan, [plan[0].File, plan[1].File]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_this_tools_earlier_attachments_are_offered_as_stale()
    {
        ExistingAttachment[] existing =
        [
            new(AttachmentId.From("old"), "Pixel diff: a (Figma | implementation | diff)"),
            new(AttachmentId.From("new"), "Pixel diff: a (Figma | implementation | diff)"),
            new(AttachmentId.From("human"), "Screenshot from QA"),
        ];

        var stale = StaleAttachments.Select(existing, [AttachmentId.From("new")]);

        Assert.Equal(["old"], stale.Select(static a => a.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_put_command_sends_every_header_verbatim_and_the_bytes_untouched()
    {
        var upload = new SignedUpload("https://uploads.example/x?sig=1", new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = "image/png", ["x-amz-acl"] = "private" });

        Assert.Equal(
            ["curl", "-sS", "--fail", "-X", "PUT", "--data-binary", "@/runs/a.png", "-H", "Content-Type: image/png", "-H", "x-amz-acl: private", "https://uploads.example/x?sig=1"],
            PutCommand.Build(upload, "/runs/a.png"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_linear_comment_tabulates_diff_geometry_and_known_differences()
    {
        var summary = A.ParitySummary().Verified("abc", designNode: null)
            .WithFrame(ParitySummaryBuilder.Frame("a", checks: [_passed], known: ["icon"]))
            .WithFrame(ParitySummaryBuilder.Frame("b", checks: [_passed, _failed]))
            .WithFrame(ParitySummaryBuilder.Frame("c"))
            .Build();

        Assert.Equal(
            "**Design parity** — build abc, viewport 1512x982, design node unspecified\n\n" +
            "| State | Pixel diff | Geometry | Known differences |\n|---|---|---|---|\n" +
            "| a | 3% (3/100) | pass | icon |\n| b | 3% (3/100) | FAIL | — |\n| c | 3% (3/100) | — | — |",
            ParityComments.Linear(summary));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_github_comment_is_text_only_and_lists_geometry_failures()
    {
        var summary = A.ParitySummary().WithProvenance(Provenance.Scrubbed).WithFrame(ParitySummaryBuilder.Frame("b", checks: [_failed])).Build();

        var comment = ParityComments.GitHub(summary);

        Assert.Equal(
            "## Design parity (scrubbed data, images on Linear)\n\n| State | Pixel diff | Known differences |\n|---|---|---|\n| b | 3% | — |\n\n" +
            "Geometry failures: b: testId:save (w 42 != 40)",
            comment);
        Assert.DoesNotContain(".png", comment, StringComparison.Ordinal);
    }
}
