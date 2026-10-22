namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>How a new repo entry meets the ones already in the profile.</summary>
internal enum RepoMatch
{
    /// <summary>Nothing with its name or its path.</summary>
    None,

    /// <summary>The same name and the same path: it is there already.</summary>
    Same,

    /// <summary>Another entry has its name or its path.</summary>
    Other,
}
