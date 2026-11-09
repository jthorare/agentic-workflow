namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>One memory store as the API shows it.</summary>
/// <param name="Name">The name.</param>
/// <param name="Description">What it is for, or null.</param>
/// <param name="CreatedBy">The actor that made it.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="Facts">How many facts it holds.</param>
internal sealed record MemoryView(string Name, string? Description, string CreatedBy, DateTimeOffset CreatedAt, int Facts);
