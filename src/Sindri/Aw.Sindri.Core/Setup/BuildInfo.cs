namespace Aw.Sindri.Core.Setup;

/// <summary>A build folder's 'build-info' (scripts/install-cs.sh writes it last, so a folder without one is unfinished or pruned): its commit and when it was built.</summary>
/// <param name="Commit">The commit it was built from.</param>
/// <param name="Built">When it was built (UTC).</param>
public sealed record BuildInfo(string Commit, DateTimeOffset Built)
{
    /// <summary>The commit as the serve step shows it: its first 7 characters.</summary>
    public string ShortCommit => Commit.Length > 7 ? Commit[..7] : Commit;
}
