namespace Aw.Sindri.Core.Checks;

/// <summary>What a branch did to a path between two commits.</summary>
public enum ChangeKind
{
    /// <summary>The path is new.</summary>
    Added = 1,

    /// <summary>The path existed and its content changed.</summary>
    Modified = 2,

    /// <summary>The path is gone.</summary>
    Deleted = 3,
}
