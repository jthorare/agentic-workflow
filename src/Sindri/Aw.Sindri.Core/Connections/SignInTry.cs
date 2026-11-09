namespace Aw.Sindri.Core.Connections;

/// <summary>One sign-in try of a connection: its name and its number, which grows with each try of that name.</summary>
/// <param name="Name">The connection name as text.</param>
/// <param name="Number">The try's number; a larger number is a newer try.</param>
public sealed record SignInTry(string Name, long Number);
