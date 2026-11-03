using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Unit)]
public sealed class FakeImageScalerContractTests : ImageScalerContract
{
    protected override IImageScaler Create() => new FakeImageScaler();

    protected override ImageAttachment Image(int width, int height, ImageFormat format) =>
        JevImages.Attachment("/fake/a", format == ImageFormat.Png ? ImageBytes.Png(width, height) : ImageBytes.Jpeg(width, height), width, height, format);
}
