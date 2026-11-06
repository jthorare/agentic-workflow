namespace Aw.Forge.Core.Pulls;

/// <summary>What a pull request did to a file.</summary>
public enum PullFileStatus
{
    /// <summary>The file is new.</summary>
    Added = 1,

    /// <summary>The file's content changed.</summary>
    Modified = 2,

    /// <summary>The file was deleted.</summary>
    Removed = 3,

    /// <summary>The file moved (and may have changed); <see cref="PullFile.PreviousPath"/> names where from.</summary>
    Renamed = 4,

    /// <summary>The file was copied from <see cref="PullFile.PreviousPath"/>.</summary>
    Copied = 5,

    /// <summary>Only the file's mode or type changed.</summary>
    Changed = 6,

    /// <summary>The file is listed but its content is the same.</summary>
    Unchanged = 7,
}
