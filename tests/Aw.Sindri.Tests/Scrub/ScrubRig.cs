using Aw.Redaction;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Infrastructure.Text;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>The real scrub service and hook slot over the real git adapter and the built-in redaction patterns.</summary>
internal static class ScrubRig
{
    internal static ScrubService Service() => new(new RedactingScrubber(Redactor.Default), new GitPreCommit(), Installer());

    internal static PreCommitRepoHooks Hooks() => new(Installer(), new GitPreCommit());

    private static PreCommitInstaller Installer() => new(new GitPreCommit());
}
