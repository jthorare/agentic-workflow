using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>A region that is painted over in both frames before the compare.</summary>
/// <param name="X">Left edge, in pixels.</param>
/// <param name="Y">Top edge, in pixels.</param>
/// <param name="W">Width, in pixels.</param>
/// <param name="H">Height, in pixels.</param>
/// <param name="Why">What changes there between runs (a person reads it).</param>
/// <param name="Size">The 'WxH' size the mask is for; every size when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScreenMask(int X, int Y, int W, int H, string Why, string? Size = null);
