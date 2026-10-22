namespace Aw.Sindri.Core.Setup;

/// <summary>What <see cref="IProfileRepoWriter.Append"/> did.</summary>
public enum RepoAppended
{
    /// <summary>There was no profile: the file was created with the defaults and the repo.</summary>
    Created,

    /// <summary>The repo was added to the existing file.</summary>
    Appended,

    /// <summary>The same repo (name and path) was already there: nothing changed.</summary>
    AlreadyThere,
}
