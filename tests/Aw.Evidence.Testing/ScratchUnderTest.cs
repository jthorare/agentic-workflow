using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Testing;

/// <summary>Scratch directories arranged by a <see cref="ScratchDirectoriesContract"/> subclass, and how to see whether one exists.</summary>
/// <param name="Scratch">The port under test.</param>
/// <param name="Exists">Whether a directory it made exists now.</param>
public sealed record ScratchUnderTest(IScratchDirectories Scratch, Func<string, bool> Exists);
