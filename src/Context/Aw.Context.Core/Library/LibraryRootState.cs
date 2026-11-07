namespace Aw.Context.Core.Library;

/// <summary>Where a library root stands.</summary>
public enum LibraryRootState
{
    /// <summary>No pass has finished for the root yet.</summary>
    Reading,

    /// <summary>The last pass listed the root.</summary>
    Ok,

    /// <summary>The last pass could not list the root; <see cref="LibraryRootStatus.Reason"/> says why.</summary>
    Unreadable,

    /// <summary>The profile turned the root off (<c language="text">stateDir: off</c>).</summary>
    Off,
}
