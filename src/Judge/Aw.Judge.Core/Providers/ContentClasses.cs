namespace Aw.Judge.Core.Providers;

/// <summary>The content classes' wire names, as the TS judge stores them.</summary>
public static class ContentClasses
{
    /// <summary>Every content class, in declaration order.</summary>
    public static IReadOnlyList<ContentClass> All { get; } =
        [ContentClass.Code, ContentClass.Diff, ContentClass.Brief, ContentClass.Transcript, ContentClass.MessageMeta, ContentClass.Image];

    /// <summary>The text classes: every class but image.</summary>
    public static IReadOnlyList<ContentClass> Text { get; } =
        [ContentClass.Code, ContentClass.Diff, ContentClass.Brief, ContentClass.Transcript, ContentClass.MessageMeta];

    /// <summary>The wire name, e.g. 'message-meta'.</summary>
    /// <param name="value">The class.</param>
    /// <returns>The name.</returns>
    public static string Name(ContentClass value) => value switch
    {
        ContentClass.Code => "code",
        ContentClass.Diff => "diff",
        ContentClass.Brief => "brief",
        ContentClass.Transcript => "transcript",
        ContentClass.MessageMeta => "message-meta",
        ContentClass.Image => "image",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown content class"),
    };
}
