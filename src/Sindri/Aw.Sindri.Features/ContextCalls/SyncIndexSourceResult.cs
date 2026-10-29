namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a named index sync did, small by design: the source's counts and the code it ended with (null when it pulled cleanly). A code is a row, not a failed job.</summary>
/// <param name="Source">The source id.</param>
/// <param name="Fetched">Records fetched from the source this run.</param>
/// <param name="Records">Records the index holds for the source after the run.</param>
/// <param name="Code">The error code the source ended with, or null.</param>
public sealed record SyncIndexSourceResult(string Source, int Fetched, int Records, string? Code);
