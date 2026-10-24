using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Testing;

/// <summary>An app starter arranged by a <see cref="DesktopAppsContract"/> subclass: the starter, the program to start and a folder to start it in.</summary>
/// <param name="Apps">The starter under test.</param>
/// <param name="Program">The program (a path with a directory, relative to <paramref name="WorkingDirectory"/>, or absolute).</param>
/// <param name="WorkingDirectory">The folder it starts in (absolute).</param>
public sealed record DesktopAppsUnderTest(IDesktopApps Apps, string Program, string WorkingDirectory);
