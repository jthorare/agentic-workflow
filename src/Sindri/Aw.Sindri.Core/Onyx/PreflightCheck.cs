using Aw.Kernel;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Core.Onyx;

/// <summary>One preflight line: the check, its verdict, what it found, and the error with its fix when it failed.</summary>
/// <param name="Name">docker, compose, memory, disk or port.</param>
/// <param name="State">The verdict.</param>
/// <param name="Detail">What it found.</param>
/// <param name="Problem">The SND-ONYX error for a failed check (its message carries the fix), otherwise null.</param>
public sealed record PreflightCheck(string Name, CheckState State, string Detail, Error? Problem);
