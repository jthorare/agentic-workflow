using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Testing;

/// <summary>Commands arranged by a <see cref="DesktopCommandsContract"/> subclass: the runner, the program to run and a folder to run it in.</summary>
/// <param name="Commands">The runner under test.</param>
/// <param name="Program">The program (a path with a directory, relative to <paramref name="WorkingDirectory"/>, or absolute).</param>
/// <param name="WorkingDirectory">The folder it runs in (absolute).</param>
public sealed record DesktopCommandsUnderTest(IDesktopCommands Commands, string Program, string WorkingDirectory);
