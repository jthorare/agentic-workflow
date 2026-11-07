namespace Aw.Sindri.Host.Commands;

/// <summary>One library folder that can't be read.</summary>
/// <param name="Root">The folder.</param>
/// <param name="Reason">Why.</param>
internal sealed record LibraryUnreadableRow(string Root, string Reason);
