using Aw.Judge.Core.Images;
using SkiaSharp;

namespace Aw.Judge.Tests.Images;

/// <summary>Real, decodable PNG and JPEG files for the scaler tests: a white page with a blue box, drawn by SkiaSharp.</summary>
internal static class SkiaImages
{
    public static byte[] Encode(int width, int height, ImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.SteelBlue };
            canvas.DrawRect(width / 8f, height / 8f, width / 2f, height / 3f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = format == ImageFormat.Png ? image.Encode(SKEncodedImageFormat.Png, 100) : image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>The same file as an attachment, its size and format read from its header like the image reader does.</summary>
    public static ImageAttachment Attachment(string path, int width, int height, ImageFormat format) =>
        Providers.JevImages.Attachment(path, Encode(width, height, format), width, height, format);
}
