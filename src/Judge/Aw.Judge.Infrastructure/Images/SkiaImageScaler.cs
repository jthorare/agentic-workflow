using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using SkiaSharp;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>
/// IImageScaler over SkiaSharp: Mitchell cubic resampling, PNG in and PNG out, JPEG in and JPEG out at quality 90. The size is worked out
/// from the header sizes alone, so an image that fits (or can't be fitted) never touches the native library. A native library that
/// won't load maps to "can't fit" (null) with one warning per scaler, never a crash.
/// </summary>
internal sealed class SkiaImageScaler : IImageScaler
{
    private const int JpegQuality = 90;

    private readonly Action _loadNative;
    private readonly Action<string> _warn;
    private int _warned;
    private volatile bool _loaded;
    private volatile bool _broken;

    /// <summary>Scales with the real native library and writes its warning to stderr.</summary>
    public SkiaImageScaler()
        : this(loadNative: TouchSkia, warn: static message => Console.Error.WriteLine("judge: warning: " + message))
    {
    }

    /// <summary>Scales with a seam for the native load and the warning.</summary>
    /// <param name="loadNative">Touches the native library once before the first scale; its DllNotFoundException or TypeInitializationException is the load failure.</param>
    /// <param name="warn">Receives the one warning.</param>
    public SkiaImageScaler(Action loadNative, Action<string> warn)
    {
        ArgumentNullException.ThrowIfNull(loadNative);
        ArgumentNullException.ThrowIfNull(warn);
        _loadNative = loadNative;
        _warn = warn;
    }

    /// <inheritdoc />
    public ImageAttachment? Fit(ImageAttachment image, JevLimits limits)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.Fits(image))
        {
            return image;
        }

        if (Target(image, limits) is not { } size || _broken)
        {
            return null;
        }

        try
        {
            if (!_loaded)
            {
                _loadNative();
                _loaded = true;
            }

            return Resample(image, size);
        }
        catch (Exception exception) when (exception is DllNotFoundException or TypeInitializationException or EntryPointNotFoundException or BadImageFormatException)
        {
            _broken = true;
            if (Interlocked.Exchange(ref _warned, 1) == 0)
            {
                _warn($"cannot scale images for Jev ({exception.GetType().Name}: {exception.Message.ReplaceLineEndings(" ")}); images over Jev's size limit go to the agent CLIs");
            }

            return null;
        }
    }

    // The one factor, both sides rounded down (the epsilon keeps 3024 x (1568 / 3024) at 1568); null when the short side or the pixels end up under the minimum.
    private static ImageSize? Target(ImageAttachment image, JevLimits limits)
    {
        var pixels = (double)image.Width * image.Height;
        var factor = Math.Min(1.0, Math.Min(Math.Min((double)limits.MaxSide / image.Width, (double)limits.MaxSide / image.Height), Math.Sqrt(limits.MaxPixels / pixels)));
        var width = (int)Math.Floor((image.Width * factor) + 1e-6);
        var height = (int)Math.Floor((image.Height * factor) + 1e-6);
        return Math.Min(width, height) < limits.MinSide || (long)width * height < limits.MinPixels ? null : new ImageSize(width, height);
    }

    private static ImageAttachment? Resample(ImageAttachment image, ImageSize size)
    {
        using var original = SKBitmap.Decode(image.Bytes.Span);
        if (original is null)
        {
            return null;
        }

        using var resized = original.Resize(new SKImageInfo(size.Width, size.Height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized is null)
        {
            return null;
        }

        using var skImage = SKImage.FromBitmap(resized);
        using var data = image.Format == ImageFormat.Png ? skImage.Encode(SKEncodedImageFormat.Png, 100) : skImage.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        var bytes = data.ToArray();
        return image with { Bytes = bytes, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), Width = size.Width, Height = size.Height };
    }

    // Loads libSkiaSharp: a 1 x 1 bitmap is the cheapest call that needs it.
    private static void TouchSkia()
    {
        using var bitmap = new SKBitmap(1, 1);
    }
}
