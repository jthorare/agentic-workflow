namespace Aw.Context.Core.Records;

/// <summary>What a committed batch did.</summary>
/// <param name="Written">Records written (new, or changed in version or text).</param>
/// <param name="Unchanged">Records a replay left as they were.</param>
/// <param name="Tombstoned">Records turned into tombstones.</param>
public sealed record CommitReport(int Written, int Unchanged, int Tombstoned);
