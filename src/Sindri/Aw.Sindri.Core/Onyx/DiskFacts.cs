namespace Aw.Sindri.Core.Onyx;

/// <summary>Free disk where Onyx would be set up.</summary>
/// <param name="MeasuredPath">The path measured: the asked path, or its nearest existing ancestor when it doesn't exist yet.</param>
/// <param name="FreeBytes">Bytes available to this user on the volume holding it.</param>
public sealed record DiskFacts(string MeasuredPath, long FreeBytes);
