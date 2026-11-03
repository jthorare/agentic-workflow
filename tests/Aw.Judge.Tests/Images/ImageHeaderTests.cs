using Aw.Judge.Core.Images;
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

    [Fact]
    public void A_truncated_png_is_null()
    {
        Assert.Null(ImageHeader.Read(ImageBytes.Png(1512, 982).AsSpan(..23)));
        Assert.Null(ImageHeader.Read(ImageBytes.Png(1512, 982).AsSpan(..12)));
    }

    [Fact]
    public void A_truncated_jpeg_is_null()
    {
        var jpeg = ImageBytes.Jpeg(3024, 1964);
        var sof = jpeg.AsSpan().IndexOf<byte>([0xFF, 0xC0]);

        Assert.Null(ImageHeader.Read(jpeg.AsSpan(..(sof + 8)))); // the frame header stops before its width
        Assert.Null(ImageHeader.Read(jpeg.AsSpan(..(sof + 1))));
        Assert.Null(ImageHeader.Read([0xFF, 0xD8, 0xFF]));
    }

    [Fact]
    public void A_fill_byte_before_a_marker_is_skipped()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xFF, 0xFF, .. Sof(640, 480)[1..]];

        Assert.Equal((ImageFormat.Jpeg, 640, 480), ImageHeader.Read(bytes));
    }

    [Fact]
    public void A_restart_or_standalone_marker_before_the_frame_has_no_length_to_skip()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xD0, 0xFF, 0x01, .. Sof(640, 480)];

        Assert.Equal((ImageFormat.Jpeg, 640, 480), ImageHeader.Read(bytes));
    }

    [Fact]
    public void A_segment_length_past_the_end_of_the_buffer_is_null()
    {
        Assert.Null(ImageHeader.Read([0xFF, 0xD8, 0xFF, 0xE0, 0xFF, 0xFF, 0x00, 0x00]));
        Assert.Null(ImageHeader.Read([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01, 0x00]));
        Assert.Null(ImageHeader.Read([0xFF, 0xD8, 0xFF, 0xE0, 0x00]));
    }

    [Fact]
    public void A_zero_width_or_height_is_null()
    {
        Assert.Null(ImageHeader.Read(ImageBytes.Png(0, 982)));
        Assert.Null(ImageHeader.Read(ImageBytes.Png(1512, 0)));
        Assert.Null(ImageHeader.Read([0xFF, 0xD8, .. Sof(0, 480)]));
        Assert.Null(ImageHeader.Read([0xFF, 0xD8, .. Sof(640, 0)]));
    }

    // An SOF0 segment: marker, length 17, precision, height, width, three components.
    private static byte[] Sof(int width, int height) =>
        [0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1];
}
