using Aw.Judge.Core.Processes;

namespace Aw.Judge.Testing;

/// <summary>One run a <see cref="FakeProcessRunner"/> received.</summary>
/// <param name="Request">The request.</param>
/// <param name="WorkingDirectoryFiles">The working directory's file names at the time of the run (null when it had none or it didn't exist).</param>
public sealed record ProcessCall(ProcessRequest Request, IReadOnlyList<string>? WorkingDirectoryFiles);
