namespace Aw.Context.Core.Library;

/// <summary>One library root as the doctor row and the Library page show it.</summary>
/// <param name="Root">'aw' or the vault's name.</param>
/// <param name="State">Where the root stands.</param>
/// <param name="Reason">Why it is unreadable (with its path), or null.</param>
/// <param name="Docs">The design docs the last pass found in it.</param>
/// <param name="NearMisses">The files that look like design docs but aren't read.</param>
/// <param name="LastRead">When the last pass finished, or null before the first.</param>
public sealed record LibraryRootStatus(string Root, LibraryRootState State, string? Reason, int Docs, int NearMisses, DateTimeOffset? LastRead);
