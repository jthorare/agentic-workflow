namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>A library folder that could not be read.</summary>
/// <param name="Root">The folder.</param>
/// <param name="Reason">Why ("permission denied").</param>
internal sealed record LibraryRootView(string Root, string Reason);
