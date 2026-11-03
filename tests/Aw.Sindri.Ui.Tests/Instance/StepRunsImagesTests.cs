using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>A judge choice's run says how many images it decided with (images spec 5.1): nothing at 0, and what was left out when some were.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunsImagesTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    private static StepRun Choose() =>
        new("1.1", "route", Attempt: 1, MaxAttempts: null, "passed", "engine", _start, _start.AddSeconds(2), DurationMs: 2_000, Outcome: "ui", Tokens: null, Actor: null);

    private static string Row(StepRun run) => StepRunsViewModel.Of([run], "done").Rows[0].Text;

    [Fact]
    public void Jobs_view_says_decided_with_2_images() =>
        Assert.Contains("decided with 2 images", Row(Choose() with { ImageCount = 2 }), StringComparison.Ordinal);

    [Fact]
    public void One_image_is_singular() =>
        Assert.Contains("decided with 1 image", Row(Choose() with { ImageCount = 1 }), StringComparison.Ordinal);

    [Fact]
    public void Dropped_images_are_counted_in_the_line() =>
        Assert.Contains("decided with 4 images (2 left out)", Row(Choose() with { ImageCount = 4, ImagesDropped = 2 }), StringComparison.Ordinal);

    [Fact]
    public void A_run_with_no_images_says_nothing_about_them() =>
        Assert.DoesNotContain("decided with", Row(Choose()), StringComparison.Ordinal);
}
