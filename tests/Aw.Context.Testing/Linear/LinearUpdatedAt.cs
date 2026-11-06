namespace Aw.Context.Testing.Linear;

/// <summary>
/// Which mutations move an issue's updatedAt (the issue they create, change or hang something on). Real Linear's rule is
/// a (Ph0 checks) assumption until the probe runs; change it here, in this one table, and every mutation follows.
/// </summary>
public static class LinearUpdatedAt
{
    private static readonly Dictionary<string, bool> _bumps = new(StringComparer.Ordinal)
    {
        ["issueCreate"] = true,
        ["issueUpdate"] = true,
        ["commentCreate"] = true,
        ["commentUpdate"] = true,
        ["attachmentLinkURL"] = true,
        ["issueArchive"] = true,
        ["issueLabelCreate"] = false,
    };

    /// <summary>The table: mutation name to whether it moves the issue's updatedAt.</summary>
    public static IReadOnlyDictionary<string, bool> Table => _bumps;

    /// <summary>Whether the mutation moves the issue's updatedAt.</summary>
    /// <param name="mutation">The mutation name.</param>
    /// <returns>True when it does.</returns>
    public static bool Bumps(string mutation) => _bumps.GetValueOrDefault(mutation);
}
