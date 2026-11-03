using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Unit)]
public sealed class ImageHeaderTests
{
    [Fact]
    public void ImageReader_reads_png_and_jpeg_sizes_from_headers()
    {
        Assert.Equal((ImageFormat.Png, 1512, 982), ImageHeader.Read(ImageBytes.Png(1512, 982)));
        Assert.Equal((ImageFormat.Jpeg, 3024, 1964), ImageHeader.Read(ImageBytes.Jpeg(3024, 1964)));
        Assert.Null(ImageHeader.Read(ImageBytes.Gif()));
    }
}
