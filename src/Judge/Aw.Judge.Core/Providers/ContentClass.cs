namespace Aw.Judge.Core.Providers;

/// <summary>What kind of content a question's input is; routes the provider chain. Wire names in <see cref="ContentClasses"/>.</summary>
public enum ContentClass
{
    /// <summary>'code'.</summary>
    Code = 1,

    /// <summary>'diff'.</summary>
    Diff = 2,

    /// <summary>'brief'.</summary>
    Brief = 3,

    /// <summary>'transcript'.</summary>
    Transcript = 4,

    /// <summary>'message-meta'.</summary>
    MessageMeta = 5,

    /// <summary>'image'.</summary>
    Image = 6,
}
