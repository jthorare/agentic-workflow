using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>A failed sign-in as <see cref="SignInOutcomes"/> keeps it: the error, already cut to its code and title, and when it failed.</summary>
/// <param name="Error">The failure, safe to show.</param>
/// <param name="At">When it failed.</param>
public sealed record SignInFailure(Error Error, DateTimeOffset At);
