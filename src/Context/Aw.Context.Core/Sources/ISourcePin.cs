namespace Aw.Context.Core.Sources;

/// <summary>What a source asks of the pin a query is scoped to (concept pins spec §3.2). The pin itself lives in Concepts, which depends on Sources, so a source sees only this.</summary>
public interface ISourcePin
{
    /// <summary>Whether a record is in the pin; one that is not is counted by the pin it sits in.</summary>
    /// <param name="record">The record.</param>
    /// <returns>True when the pin holds it.</returns>
    public bool HoldsRecord(RecordKey record);

    /// <summary>Whether a repo doc is in the pin: its record is linked in, or failing a link its path is inside the pin's code paths; one that is not is counted.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="repoRelativePath">The path in the repo.</param>
    /// <returns>True when the pin holds it.</returns>
    public bool HoldsDoc(string repo, string repoRelativePath);

    /// <summary>The records of the map the pin holds (the ones the dense leg's scan is cut to).</summary>
    /// <returns>Their keys.</returns>
    public IReadOnlySet<RecordKey> Records();
}
