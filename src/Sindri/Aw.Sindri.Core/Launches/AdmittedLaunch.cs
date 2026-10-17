namespace Aw.Sindri.Core.Launches;

/// <summary>The answer of <see cref="ReportedIdAdmission"/>.</summary>
/// <param name="Item">The launch's item id, or null.</param>
/// <param name="Accepted">Whether the id may be recorded.</param>
public sealed record AdmittedLaunch(string? Item, bool Accepted);
