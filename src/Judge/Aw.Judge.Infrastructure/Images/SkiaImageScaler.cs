using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>
/// IImageScaler over SkiaSharp: Mitchell cubic resampling, PNG in and PNG out, JPEG in and JPEG out at quality 90. A native library that
/// won't load maps to "can't fit" (null) with one warning on stderr per process, never a crash.
/// </summary>
internal sealed class SkiaImageScaler : IImageScaler
{
    /// <summary>Scales with the real native library and writes its warning to stderr.</summary>
    public SkiaImageScaler()
        : this(loadNative: static () => { }, warn: static _ => { })
    {
    }

    /// <summary>Scales with a seam for the native load and the warning.</summary>
    /// <param name="loadNative">Touches the native library once before the first scale; its DllNotFoundException or TypeInitializationException is the load failure.</param>
    /// <param name="warn">Receives the one warning per process.</param>
    public SkiaImageScaler(Action loadNative, Action<string> warn)
    {
        ArgumentNullException.ThrowIfNull(loadNative);
        ArgumentNullException.ThrowIfNull(warn);
    }

    /// <inheritdoc />
    public ImageAttachment? Fit(ImageAttachment image, JevLimits limits) => throw new NotSupportedException("SkiaImageScaler is not built yet");
}
