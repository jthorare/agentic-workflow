namespace Aw.Judge.Core.Questions;

/// <summary>The image formats the judge reads, by magic bytes: nothing else is sent to a model.</summary>
public enum ImageFormat
{
    /// <summary>PNG (89 50 4E 47).</summary>
    Png,

    /// <summary>JPEG (FF D8 FF).</summary>
    Jpeg,
}
