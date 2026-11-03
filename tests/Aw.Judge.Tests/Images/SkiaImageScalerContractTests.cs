using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

/// <summary>The real scaler over real files and the native SkiaSharp library, so the integration lane.</summary>
[Lane(TestLane.Integration)]
public sealed class SkiaImageScalerContractTests : ImageScalerContract
{
    protected override IImageScaler Create() => new SkiaImageScaler();

    protected override ImageAttachment Image(int width, int height, ImageFormat format) => SkiaImages.Attachment("/skia/a", width, height, format);
}
