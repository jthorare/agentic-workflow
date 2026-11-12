namespace Aw.Sindri.Core.Events;

/// <summary>The 'resource.purged' payload (#2941): an owner's resources were deleted. The envelope's actor is who purged them. Counts and bytes only, never file names.</summary>
/// <param name="Owner">The owner, as 'kind:key'.</param>
/// <param name="Databases">How many databases were deleted.</param>
/// <param name="Disks">How many disk folders were deleted.</param>
/// <param name="StrayFiles">How many other entries were deleted.</param>
/// <param name="BytesFreed">The bytes the deleted entries held; null when any of them could not be measured.</param>
/// <param name="At">When they were deleted.</param>
public sealed record ResourcePurged(string Owner, int Databases, int Disks, int StrayFiles, long? BytesFreed, DateTimeOffset At);
