using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Images;
using Aw.Kernel;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>
/// Renders each screen's mockup in the browser at the size the app was driven at, compares the two frames pixel by pixel and writes the
/// screen's design, diff and side-by-side images beside the app's frame.
/// </summary>
internal sealed class DesktopFrames(IBrowser browser, IStaticSite site, IRunFiles files, IImageCodec codec)
{
    private static readonly TimeSpan _load = TimeSpan.FromSeconds(30);

    internal async ValueTask<Result<IReadOnlyList<DesktopScreenResult>>> CompareAsync(DesktopPlan plan, IReadOnlyDictionary<string, string> problems, CancellationToken cancellationToken)
    {
        var designs = await RenderAsync(plan, cancellationToken).ConfigureAwait(false);
        if (designs.IsError)
        {
            return designs.Error;
        }

        List<DesktopScreenResult> results = [];
        foreach (var screen in plan.Screens)
        {
            foreach (var size in plan.SizesOf(screen))
            {
                var result = await CompareOneAsync(plan, new Frame(screen, size, designs.Value[DesktopPlan.KeyOf(screen, size)], problems.GetValueOrDefault(DesktopPlan.KeyOf(screen, size))), cancellationToken).ConfigureAwait(false);
                if (result.IsError)
                {
                    return result.Error;
                }

                results.Add(result.Value);
            }
        }

        return results;
    }

    internal ValueTask<Result<Unit>> WriteReportAsync(string runDirectory, DesktopSummary summary, CancellationToken cancellationToken) =>
        files.WriteAsync(RunPath.Combine(runDirectory, "report.md"), Encoding.UTF8.GetBytes(DesktopReport.Markdown(summary)), cancellationToken);

    private async ValueTask<Result<Dictionary<string, byte[]>>> RenderAsync(DesktopPlan plan, CancellationToken cancellationToken)
    {
        var served = await site.ServeAsync(plan.DesignDirectory, cancellationToken).ConfigureAwait(false);
        if (served.IsError)
        {
            return served.Error;
        }

        Dictionary<string, byte[]> frames = new(StringComparer.Ordinal);
        await using (served.Value.ConfigureAwait(false))
        {
            var host = new ResolvedHost(served.Value.Origin, HostKind.Local);
            foreach (var size in plan.Sizes)
            {
                var rendered = await RenderSizeAsync(plan, new BrowserSessionOptions(host, size, RecordDirectory: null, UserAgentOverride: null), frames, cancellationToken).ConfigureAwait(false);
                if (rendered.IsError)
                {
                    return rendered.Error;
                }
            }
        }

        return frames;
    }

    private async ValueTask<Result<Unit>> RenderSizeAsync(DesktopPlan plan, BrowserSessionOptions options, Dictionary<string, byte[]> frames, CancellationToken cancellationToken)
    {
        var opened = await browser.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            foreach (var screen in plan.At(options.Viewport))
            {
                var shot = await ShootAsync(opened.Value, screen, cancellationToken).ConfigureAwait(false);
                if (shot.IsError)
                {
                    return EvidenceErrors.RunFailed.With($"design {screen.Name} at {options.Viewport.Describe()}: {shot.Error.Message}");
                }

                frames[DesktopPlan.KeyOf(screen, options.Viewport)] = shot.Value;
            }
        }

        return Unit.Value;
    }

    private static async ValueTask<Result<byte[]>> ShootAsync(IBrowserSession session, DesktopScreen screen, CancellationToken cancellationToken)
    {
        var settle = TimeSpan.FromMilliseconds(screen.SettleMs ?? DesktopScreen.DefaultSettleMs);
        var gone = await session.GotoAsync("/" + screen.Design, _load, cancellationToken).ConfigureAwait(false);
        var settled = gone.IsError ? gone : await session.SettleAsync(settle, cancellationToken).ConfigureAwait(false);
        return settled.IsError ? settled.Error : await session.ScreenshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<DesktopScreenResult>> CompareOneAsync(DesktopPlan plan, Frame frame, CancellationToken cancellationToken)
    {
        var paths = plan.FilesOf(frame.Screen, frame.Size);
        var masks = MasksOf(frame.Screen, frame.Size);
        var threshold = plan.ThresholdOf(frame.Screen);
        if (frame.Problem is { } scripted)
        {
            return Unreadable(frame, paths, threshold, masks.Count, scripted);
        }

        var app = await files.ReadAsync(paths.App, cancellationToken).ConfigureAwait(false);
        if (app.IsError)
        {
            return Unreadable(frame, paths, threshold, masks.Count, $"the screenshot wrote no frame: {app.Error.Message}");
        }

        var appImage = codec.Decode(app.Value);
        var designImage = codec.Decode(frame.Design);
        var compared = appImage.Bind(a => designImage.Bind(d => ScreenComparison.Compare(a, d, masks)));
        if (compared.IsError)
        {
            return Unreadable(frame, paths, threshold, masks.Count, compared.Error.Message);
        }

        var written = await WriteAsync(paths, frame.Design, compared.Value, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new DesktopScreenResult(
                frame.Screen.Name, frame.Size.Describe(), compared.Value.Pixels.DiffPercent, threshold, compared.Value.Within(threshold), compared.Value.Problem, paths,
                [.. frame.Screen.Accepted ?? []], masks.Count);
    }

    private static DesktopScreenResult Unreadable(Frame frame, DesktopFiles paths, double threshold, int masked, string problem) =>
        new(frame.Screen.Name, frame.Size.Describe(), DiffPercent: 0, threshold, Passed: false, problem, paths, [.. frame.Screen.Accepted ?? []], masked);

    private async ValueTask<Result<Unit>> WriteAsync(DesktopFiles paths, byte[] design, ScreenComparison compared, CancellationToken cancellationToken)
    {
        var sideBySide = ImageOps.SideBySide(compared.Design, compared.App, compared.Pixels.Diff);
        (string Path, byte[] Bytes)[] outputs = [(paths.Design, design), (paths.Diff, codec.Encode(compared.Pixels.Diff)), (paths.SideBySide, codec.Encode(sideBySide))];
        foreach (var (path, bytes) in outputs)
        {
            var written = await files.WriteAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return Unit.Value;
    }

    private static List<Region> MasksOf(DesktopScreen screen, ViewportSize size) =>
        [.. (screen.Masks ?? []).Where(m => m.Size is null || string.Equals(m.Size, size.Describe(), StringComparison.Ordinal)).Select(static m => new Region(m.X, m.Y, m.W, m.H))];

    private sealed record Frame(DesktopScreen Screen, ViewportSize Size, byte[] Design, string? Problem);
}
